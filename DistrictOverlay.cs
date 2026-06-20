using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal sealed class DistrictOverlay : Form
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct POINT
	{
		public int X;

		public int Y;
	}

	private struct WorldObjectSnapshot
	{
		public string Name;

		public float X;

		public float Y;

		public float Z;

		public int Role;

		public WorldPoint[] Points;
	}

	private struct WorldPoint
	{
		public float X;

		public float Y;

		public float Z;
	}

	private const int GWL_EXSTYLE = -20;

	private const long OFF_FAKE_DM_PTR = 129824424L;

	private const long OFF_FAKE_TO_DM = 472L;

	private const long OFF_WORKSPACE = 376L;

	private const long OFF_CHILDREN = 120L;

	private const long OFF_CLASS_DESC = 24L;

	private const long OFF_CLASS_NAME = 8L;

	private const long OFF_NAME = 176L;

	private const long OFF_LOCAL_PLAYER = 328L;

	private const long OFF_CAMERA = 1192L;

	private const long OFF_HEALTH = 404L;

	private const long OFF_MAXHEALTH = 436L;

	private const long OFF_CAM_ROTATION = 248L;

	private const long OFF_CAM_POS = 284L;

	private const long OFF_FOV = 352L;

	private static IntPtr robloxWindow;

	private readonly List<TrackedEntity> trackedEntities = new List<TrackedEntity>();

	public static bool ShowKillerEsp { get; set; } = true;

	public static bool ShowGeneratorEsp { get; set; }

	public static bool ShowPalletEsp { get; set; }

	public static bool ShowHookEsp { get; set; }

	public static bool ShowExitGateEsp { get; set; }

	private static long dataModel;

	private static long workspace;

	private static long players;

	private static long localPlayer;

	private static long camera;

	private static string autoDetectedKillerName = "";

	private static readonly Dictionary<string, int> cachedCombatRoles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, int> lastCombatRoleScan = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private static readonly List<WorldObjectSnapshot> worldObjectCache = new List<WorldObjectSnapshot>();

	private static long lastStaticScan = 0L;

	private int overlayWidth = 1920;

	private int overlayHeight = 1080;

	private volatile bool isRunning;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 524320;
			return createParams;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buffer, int size, out int read);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hwnd, int index, int newLong);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("user32.dll")]
	private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

	private static long ReadPtr(long address)
	{
		byte[] array = new byte[8];
		ReadProcessMemory(DistrictClient.ProcessHandle, new IntPtr(address), array, 8, out var _);
		return BitConverter.ToInt64(array, 0);
	}

	private static float ReadFloat(long address)
	{
		byte[] array = new byte[4];
		ReadProcessMemory(DistrictClient.ProcessHandle, new IntPtr(address), array, 4, out var _);
		return BitConverter.ToSingle(array, 0);
	}

	private static float[] ReadFloats(long address, int count)
	{
		byte[] array = new byte[count * 4];
		ReadProcessMemory(DistrictClient.ProcessHandle, new IntPtr(address), array, array.Length, out var _);
		float[] array2 = new float[count];
		for (int i = 0; i < count; i++)
		{
			array2[i] = BitConverter.ToSingle(array, i * 4);
		}
		return array2;
	}

	private static string ReadStr(long address, int maxLen)
	{
		byte[] array = new byte[maxLen];
		ReadProcessMemory(DistrictClient.ProcessHandle, new IntPtr(address), array, maxLen, out var _);
		int num = Array.IndexOf(array, (byte)0);
		if (num < 0)
		{
			num = maxLen;
		}
		if (num == 0)
		{
			return "";
		}
		return Encoding.ASCII.GetString(array, 0, num);
	}

	private static string GetClassName(long instance)
	{
		if (instance == 0)
		{
			return "";
		}
		long descriptor = ReadPtr(instance + OFF_CLASS_DESC);
		if (descriptor == 0)
		{
			return "";
		}
		long classNameAddress = ReadPtr(descriptor + OFF_CLASS_NAME);
		if (classNameAddress == 0)
		{
			return "";
		}
		return ReadStr(classNameAddress, 64);
	}

	private static string GetInstanceName(long instance)
	{
		if (instance == 0)
		{
			return "";
		}
		long stringObject = ReadPtr(instance + OFF_NAME);
		if (stringObject == 0)
		{
			return "?";
		}
		long length = ReadPtr(stringObject + 16);
		if (length == 0)
		{
			return "";
		}
		if (length < 0 || length > 1000)
		{
			return "?";
		}
		long dataAddress = length < 16 ? stringObject : ReadPtr(stringObject);
		string value = ReadStr(dataAddress, (int)length);
		for (int i = 0; i < value.Length; i++)
		{
			if (char.IsControl(value[i]) || value[i] == '\0')
			{
				value = value.Substring(0, i);
				break;
			}
		}
		value = value.Trim();
		if (value.Length == 0)
		{
			return "?";
		}
		return value;
	}

	private static List<long> GetChildren(long instance)
	{
		List<long> children = new List<long>();
		if (instance == 0)
		{
			return children;
		}
		long childrenContainer = ReadPtr(instance + OFF_CHILDREN);
		if (childrenContainer == 0)
		{
			return children;
		}
		long start = ReadPtr(childrenContainer);
		long end = ReadPtr(childrenContainer + 8);
		if (start == 0L || end == 0L || end <= start)
		{
			return children;
		}
		int count = Math.Min(500, (int)((end - start) / 16));
		for (int i = 0; i < count; i++)
		{
			long child = ReadPtr(start + i * 16);
			if (child != 0)
			{
				children.Add(child);
			}
		}
		return children;
	}

	private static long FindChildByClass(long parent, string cn)
	{
		List<long> children = GetChildren(parent);
		for (int i = 0; i < children.Count; i++)
		{
			if (GetClassName(children[i]) == cn)
			{
				return children[i];
			}
		}
		return 0L;
	}

	private static long FindChildByName(long parent, string name)
	{
		List<long> children = GetChildren(parent);
		for (int i = 0; i < children.Count; i++)
		{
			if (GetInstanceName(children[i]) == name)
			{
				return children[i];
			}
		}
		return 0L;
	}

	private bool WorldToScreen(float wx, float wy, float wz, float[] camRot, float[] camPos, float fov, int screenW, int screenH, out int sx, out int sy)
	{
		sx = 0;
		sy = 0;
		float num = camRot[0];
		float num2 = camRot[3];
		float num3 = camRot[6];
		float num4 = camRot[1];
		float num5 = camRot[4];
		float num6 = camRot[7];
		float num7 = camRot[2];
		float num8 = camRot[5];
		float num9 = camRot[8];
		float num10 = wx - camPos[0];
		float num11 = wy - camPos[1];
		float num12 = wz - camPos[2];
		float num13 = 0f - (num10 * num7 + num11 * num8 + num12 * num9);
		float num14 = num10 * num + num11 * num2 + num12 * num3;
		float num15 = num10 * num4 + num11 * num5 + num12 * num6;
		if (num13 <= 0.1f)
		{
			return false;
		}
		float num16 = (float)screenH / 2f / (float)Math.Tan(fov / 2f);
		sx = (int)((float)screenW / 2f + num14 / num13 * num16);
		sy = (int)((float)screenH / 2f - num15 / num13 * num16);
		return sx >= -100 && sx <= screenW + 100 && sy >= -100 && sy <= screenH + 100;
	}

	internal DistrictOverlay()
	{
		isRunning = true;
		Text = "Violence District Overlay";
		base.FormBorderStyle = FormBorderStyle.None;
		BackColor = Color.Black;
		base.TransparencyKey = Color.Black;
		base.TopMost = true;
		base.ShowInTaskbar = false;
		DoubleBuffered = true;
		base.StartPosition = FormStartPosition.Manual;
		int windowLong = GetWindowLong(base.Handle, GWL_EXSTYLE);
		SetWindowLong(base.Handle, GWL_EXSTYLE, windowLong | 0x80000 | 0x20);
		ThreadStart start = delegate
		{
			while (isRunning)
			{
				if (robloxWindow != IntPtr.Zero)
				{
					try
					{
						UpdatePlayers();
						Invoke((MethodInvoker)delegate
						{
							OnUpdate(null, null);
						});
					}
					catch
					{
					}
				}
				Thread.Sleep(16);
			}
		};
		Thread thread = new Thread(start);
		thread.IsBackground = true;
		thread.Start();
	}

	protected override void Dispose(bool disposing)
	{
		isRunning = false;
		base.Dispose(disposing);
	}

	private void OnUpdate(object sender, EventArgs e)
	{
		if (!GetClientRect(robloxWindow, out var rect))
		{
			return;
		}
		POINT point = default(POINT);
		ClientToScreen(robloxWindow, ref point);
		base.Location = new Point(point.X, point.Y);
		base.Size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
		overlayWidth = base.Width;
		overlayHeight = base.Height;
		IntPtr foregroundWindow = GetForegroundWindow();
		StringBuilder stringBuilder = new StringBuilder(256);
		if (GetWindowText(foregroundWindow, stringBuilder, 256) > 0)
		{
			string text = stringBuilder.ToString().ToLower();
			if (text.Contains("roblox") || foregroundWindow == base.Handle)
			{
				if (!base.Visible)
				{
					base.Visible = true;
				}
			}
			else if (base.Visible)
			{
				base.Visible = false;
			}
		}
		else if (foregroundWindow != robloxWindow && foregroundWindow != base.Handle)
		{
			if (base.Visible)
			{
				base.Visible = false;
			}
		}
		else if (!base.Visible)
		{
			base.Visible = true;
		}
		if (DistrictClient.AimbotEnabled)
		{
			short asyncKeyState = GetAsyncKeyState(2);
			if ((asyncKeyState & 0x8000) != 0)
			{
				TrackedEntity playerInfo = null;
				float num = float.MaxValue;
				int num2 = base.Width / 2;
				int num3 = base.Height / 2;
				foreach (TrackedEntity player in trackedEntities)
				{
					if (player.OnScreen && player.Role == 2)
					{
						float num4 = player.ScreenX - num2;
						float num5 = player.ScreenY - num3;
						float num6 = (float)Math.Sqrt(num4 * num4 + num5 * num5);
						if (num6 < num)
						{
							num = num6;
							playerInfo = player;
						}
					}
				}
				if (playerInfo != null)
				{
					int dx = (int)((float)(playerInfo.ScreenX - num2) / 4f);
					int dy = (int)((float)(playerInfo.ScreenY - num3) / 4f);
					mouse_event(1, dx, dy, 0, 0);
				}
			}
		}
		Invalidate();
	}

	private static bool IsStaticPartClass(string className)
	{
		return className == "Part" || className == "MeshPart" || className == "UnionOperation" ||
			className == "TrussPart" || className == "WedgePart" || className == "CornerWedgePart" ||
			className == "Seat" || className == "VehicleSeat";
	}

	private static bool TryGetPartPosition(long part, out WorldPoint point)
	{
		point = default(WorldPoint);
		long primitive = ReadPtr(part + 328);
		if (primitive <= 1099511627776L || primitive >= 140737488355327L)
		{
			return false;
		}
		float x = ReadFloat(primitive + 308);
		float y = ReadFloat(primitive + 312);
		float z = ReadFloat(primitive + 316);
		if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
			float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
		{
			return false;
		}
		point.X = x;
		point.Y = y;
		point.Z = z;
		return true;
	}

	private static void CollectObjectPoints(long instance, int depth, List<WorldPoint> points)
	{
		if (depth > 6 || points.Count >= 96)
		{
			return;
		}
		List<long> children = GetChildren(instance);
		for (int i = 0; i < children.Count && points.Count < 96; i++)
		{
			long child = children[i];
			string className = GetClassName(child);
			if (IsStaticPartClass(className))
			{
				WorldPoint point;
				if (TryGetPartPosition(child, out point))
				{
					points.Add(point);
				}
			}
			else if (className == "Model" || className == "Folder")
			{
				CollectObjectPoints(child, depth + 1, points);
			}
		}
	}

	private static int GetStaticObjectRole(string name)
	{
		string text = name.ToLowerInvariant();
		if (text.Contains("generator") || text == "gen" || text.StartsWith("gen_") || text.EndsWith("_gen"))
		{
			return 3;
		}
		if (text.Contains("pallet"))
		{
			return 4;
		}
		if (text.Contains("hook"))
		{
			return 5;
		}
		if (text.Contains("gate") || text.Contains("exit") || text.Contains("escape") ||
			text.Contains("maindoor") || text.Contains("escape door"))
		{
			return 6;
		}
		return 0;
	}

	private void ScanStaticObjects(long parent, int depth)
	{
		if (depth > 6)
		{
			return;
		}
		List<long> children = GetChildren(parent);
		for (int i = 0; i < children.Count; i++)
		{
			long instance = children[i];
			string className = GetClassName(instance);
			if (className != "Folder" && className != "Model")
			{
				continue;
			}
			string instanceName = GetInstanceName(instance);
			int role = GetStaticObjectRole(instanceName ?? "");
			// Plural folder names usually group every object of that type on the map.
			bool isContainer = string.Equals(instanceName, "Generators", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(instanceName, "Pallets", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(instanceName, "Hooks", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(instanceName, "Gates", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(instanceName, "Exits", StringComparison.OrdinalIgnoreCase);
			if (role != 0 && !isContainer)
			{
				List<WorldPoint> points = new List<WorldPoint>();
				CollectObjectPoints(instance, 0, points);
				if (points.Count > 0)
				{
					float x = 0f;
					float y = 0f;
					float z = 0f;
					for (int j = 0; j < points.Count; j++)
					{
						x += points[j].X;
						y += points[j].Y;
						z += points[j].Z;
					}
					worldObjectCache.Add(new WorldObjectSnapshot
					{
						Name = instanceName,
						X = x / points.Count,
						Y = y / points.Count,
						Z = z / points.Count,
						Role = role,
						Points = points.ToArray()
					});
					// Avoid duplicate entries from nested models with similar names.
					continue;
				}
			}
			ScanStaticObjects(instance, depth + 1);
		}
	}

	private static bool NameContainsAny(string name, string[] keywords)
	{
		for (int i = 0; i < keywords.Length; i++)
		{
			if (name.Contains(keywords[i]))
			{
				return true;
			}
		}
		return false;
	}

	private static readonly string[] KillerWeaponKeywords =
	{
		"knife", "dagger", "machete", "chainsaw", "hatchet", "battleaxe", "fireaxe",
		"blade", "katana", "scythe", "claw", "hammer", "club", "killerbat", "sword"
	};

	private static readonly string[] GunKeywords =
	{
		"revolver", "pistol", "firearm", "handgun", "gunscript", "gunstates"
	};

	private static void ScoreCombatDescendants(long instance, int depth, ref int killerScore, ref int gunScore, ref int visited)
	{
		if (instance == 0 || depth > 6 || visited >= 350)
		{
			return;
		}
		visited++;
		string name = GetInstanceName(instance).ToLowerInvariant();
		string className = GetClassName(instance);
		if (name.Contains("killer") || name.Contains("murderer") || name.Contains("slasher"))
		{
			killerScore += 8;
		}
		if (NameContainsAny(name, KillerWeaponKeywords))
		{
			killerScore += className == "Tool" ? 7 : 5;
		}
		if (name == "weapon" || name.Contains("killerweapon") || name.Contains("primaryattack") ||
			name.Contains("attackhitbox") || name.Contains("damagehitbox"))
		{
			killerScore += 4;
		}
		if (name == "gun" || NameContainsAny(name, GunKeywords))
		{
			gunScore += className == "Tool" ? 7 : 5;
		}
		List<long> children = GetChildren(instance);
		for (int i = 0; i < children.Count && visited < 350; i++)
		{
			ScoreCombatDescendants(children[i], depth + 1, ref killerScore, ref gunScore, ref visited);
		}
	}

	private static void CollectCharacterModels(long parent, int depth, List<long> models)
	{
		if (parent == 0 || depth > 4 || models.Count >= 64)
		{
			return;
		}
		List<long> children = GetChildren(parent);
		for (int i = 0; i < children.Count && models.Count < 64; i++)
		{
			long child = children[i];
			string className = GetClassName(child);
			if (className == "Model" && FindChildByClass(child, "Humanoid") != 0 &&
				(FindChildByName(child, "HumanoidRootPart") != 0 || FindChildByName(child, "Torso") != 0 || FindChildByName(child, "UpperTorso") != 0))
			{
				models.Add(child);
				continue;
			}
			if (className != "Folder" && className != "Model")
			{
				continue;
			}
			string name = GetInstanceName(child).ToLowerInvariant();
			bool likelyCharacterContainer = name.Contains("character") || name.Contains("player") ||
				name.Contains("survivor") || name.Contains("killer") || name == "live" ||
				name.Contains("entity") || name.Contains("actor") || name.Contains("clone");
			// Inspect one level below Workspace, then follow likely character containers only.
			if (depth == 0 || likelyCharacterContainer)
			{
				CollectCharacterModels(child, depth + 1, models);
			}
		}
	}

	private void UpdatePlayers()
	{
		List<TrackedEntity> list = new List<TrackedEntity>();
		long num = ReadPtr(DistrictClient.BaseAddress + OFF_FAKE_DM_PTR);
		if (num == 0)
		{
			return;
		}
		dataModel = ReadPtr(num + OFF_FAKE_TO_DM);
		if (dataModel == 0)
		{
			return;
		}
		workspace = ReadPtr(dataModel + OFF_WORKSPACE);
		if (workspace == 0)
		{
			return;
		}
		players = FindChildByClass(dataModel, "Players");
		if (players == 0)
		{
			return;
		}
		localPlayer = ReadPtr(players + OFF_LOCAL_PLAYER);
		camera = ReadPtr(workspace + OFF_CAMERA);
		if (camera == 0)
		{
			camera = FindChildByClass(workspace, "Camera");
		}
		if (camera == 0)
		{
			return;
		}
		float[] camRot = ReadFloats(camera + OFF_CAM_ROTATION, 9);
		float[] array = ReadFloats(camera + OFF_CAM_POS, 3);
		float fov = ReadFloat(camera + OFF_FOV);
		int num2 = overlayWidth;
		int num3 = overlayHeight;
		if (num2 <= 0 || num3 <= 0)
		{
			return;
		}
		List<long> children = GetChildren(players);
		string instanceName = GetInstanceName(localPlayer);
		List<long> children2 = new List<long>();
		CollectCharacterModels(workspace, 0, children2);
		if (Environment.TickCount - lastStaticScan > 5000)
		{
			worldObjectCache.Clear();
			ScanStaticObjects(workspace, 1);
			lastStaticScan = Environment.TickCount;
		}
		for (int i = 0; i < worldObjectCache.Count; i++)
		{
			WorldObjectSnapshot worldObject = worldObjectCache[i];
			float num4 = worldObject.X - array[0];
			float num5 = worldObject.Y - array[1];
			float num6 = worldObject.Z - array[2];
			float distance = (float)Math.Sqrt(num4 * num4 + num5 * num5 + num6 * num6);
			int sx;
			int sy;
			bool onScreen = WorldToScreen(worldObject.X, worldObject.Y, worldObject.Z, camRot, array, fov, num2, num3, out sx, out sy);
			TrackedEntity playerInfo = new TrackedEntity();
			playerInfo.Name = worldObject.Name;
			playerInfo.X = worldObject.X;
			playerInfo.Y = worldObject.Y;
			playerInfo.Z = worldObject.Z;
			playerInfo.Distance = distance;
			playerInfo.ScreenX = sx;
			playerInfo.ScreenY = sy;
			playerInfo.OnScreen = onScreen;
			playerInfo.Role = worldObject.Role;
			list.Add(playerInfo);
		}
		for (int j = 0; j < children2.Count; j++)
		{
			long instance = children2[j];
			long num7 = FindChildByClass(instance, "Humanoid");
			long num8 = FindChildByName(instance, "HumanoidRootPart");
			if (num8 == 0)
			{
				num8 = FindChildByName(instance, "Torso");
			}
			if (num8 == 0)
			{
				num8 = FindChildByName(instance, "UpperTorso");
			}
			string text = GetInstanceName(instance);
			if (string.IsNullOrEmpty(text))
			{
				text = "Unknown";
			}
			if (num7 == 0L || num8 == 0L || (instanceName != "?" && text == instanceName))
			{
				continue;
			}
			float num9 = 0f;
			float num10 = 0f;
			float num11 = 0f;
			long num12 = ReadPtr(num8 + 328);
			if (num12 > 1099511627776L && num12 < 140737488355327L)
			{
				num9 = ReadFloat(num12 + 308);
				num10 = ReadFloat(num12 + 312);
				num11 = ReadFloat(num12 + 316);
			}
			if ((num9 == 0f && num10 == 0f && num11 == 0f) || Math.Abs(num9) < 0.01f || float.IsNaN(num9))
			{
				continue;
			}
			float health = ReadFloat(num7 + OFF_HEALTH);
			float maxHealth = ReadFloat(num7 + OFF_MAXHEALTH);
			float num13 = num9 - array[0];
			float num14 = num10 - array[1];
			float num15 = num11 - array[2];
			float distance2 = (float)Math.Sqrt(num13 * num13 + num14 * num14 + num15 * num15);
			int role = 0;
			cachedCombatRoles.TryGetValue(text, out role);
			int previousScan;
			int now = Environment.TickCount;
			if (!lastCombatRoleScan.TryGetValue(text, out previousScan) || unchecked(now - previousScan) >= 750)
			{
				long num16 = FindChildByName(players, text);
				int killerScore = 0;
				int gunScore = 0;
				int visited = 0;
				ScoreCombatDescendants(instance, 0, ref killerScore, ref gunScore, ref visited);
				if (num16 != 0)
				{
					long num17 = FindChildByName(num16, "Backpack");
					if (num17 != 0)
					{
						ScoreCombatDescendants(num17, 0, ref killerScore, ref gunScore, ref visited);
					}
					ScoreCombatDescendants(num16, 0, ref killerScore, ref gunScore, ref visited);
				}
				if (killerScore >= 5 && killerScore >= gunScore)
				{
					role = 2;
					if (!string.Equals(autoDetectedKillerName, text, StringComparison.OrdinalIgnoreCase))
					{
						autoDetectedKillerName = text;
					}
				}
				else if (gunScore >= 5)
				{
					role = 1;
				}
				else if (role != 2)
				{
					role = 0;
				}
				cachedCombatRoles[text] = role;
				lastCombatRoleScan[text] = now;
			}
			if (!string.IsNullOrEmpty(autoDetectedKillerName) && string.Equals(text, autoDetectedKillerName, StringComparison.OrdinalIgnoreCase))
			{
				role = 2;
			}
			if (!string.IsNullOrEmpty(DistrictClient.ManualKillerName) && text.IndexOf(DistrictClient.ManualKillerName, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				role = 2;
			}
			int sx2;
			int sy2;
			bool onScreen2 = WorldToScreen(num9, num10, num11, camRot, array, fov, num2, num3, out sx2, out sy2);
			TrackedEntity playerInfo2 = new TrackedEntity();
			playerInfo2.Name = text;
			playerInfo2.X = num9;
			playerInfo2.Y = num10;
			playerInfo2.Z = num11;
			playerInfo2.Health = health;
			playerInfo2.MaxHealth = maxHealth;
			playerInfo2.Distance = distance2;
			playerInfo2.ScreenX = sx2;
			playerInfo2.ScreenY = sy2;
			playerInfo2.OnScreen = onScreen2;
			playerInfo2.Role = role;
			list.Add(playerInfo2);
		}
		lock (trackedEntities)
		{
			trackedEntities.Clear();
			trackedEntities.AddRange(list);
		}
	}

	private static bool IsStaticEspEnabled(int role)
	{
		return (role == 3 && ShowGeneratorEsp) || (role == 4 && ShowPalletEsp) ||
			(role == 5 && ShowHookEsp) || (role == 6 && ShowExitGateEsp);
	}

	private static Color GetStaticEspColor(int role)
	{
		if (role == 3)
		{
			return Color.FromArgb(236, 216, 72);
		}
		if (role == 4)
		{
			return Color.FromArgb(255, 193, 35);
		}
		if (role == 5)
		{
			return Color.FromArgb(255, 35, 50);
		}
		return Color.FromArgb(230, 235, 240);
	}

	private static GraphicsPath CreateFallbackStaticPath(int role, float centerX, float centerY, float scale)
	{
		GraphicsPath path = new GraphicsPath();
		if (role == 3)
		{
			float w = scale * 1.55f;
			float h = scale * 1.05f;
			PointF[] generator =
			{
				new PointF(centerX - w * 0.48f, centerY - h * 0.35f),
				new PointF(centerX - w * 0.35f, centerY - h * 0.50f),
				new PointF(centerX + w * 0.08f, centerY - h * 0.48f),
				new PointF(centerX + w * 0.16f, centerY - h * 0.62f),
				new PointF(centerX + w * 0.32f, centerY - h * 0.60f),
				new PointF(centerX + w * 0.35f, centerY - h * 0.43f),
				new PointF(centerX + w * 0.50f, centerY - h * 0.34f),
				new PointF(centerX + w * 0.45f, centerY + h * 0.42f),
				new PointF(centerX + w * 0.18f, centerY + h * 0.53f),
				new PointF(centerX - w * 0.38f, centerY + h * 0.50f)
			};
			path.AddPolygon(generator);
		}
		else if (role == 4)
		{
			float w = scale * 0.95f;
			float h = scale * 1.35f;
			path.AddPolygon(new PointF[]
			{
				new PointF(centerX - w * 0.42f, centerY - h * 0.55f),
				new PointF(centerX + w * 0.54f, centerY - h * 0.42f),
				new PointF(centerX + w * 0.38f, centerY + h * 0.56f),
				new PointF(centerX - w * 0.55f, centerY + h * 0.38f)
			});
		}
		else if (role == 5)
		{
			float w = scale * 0.55f;
			float h = scale * 1.75f;
			path.AddPolygon(new PointF[]
			{
				new PointF(centerX - w * 0.14f, centerY + h * 0.52f),
				new PointF(centerX + w * 0.18f, centerY + h * 0.52f),
				new PointF(centerX + w * 0.08f, centerY - h * 0.34f),
				new PointF(centerX + w * 0.48f, centerY - h * 0.26f),
				new PointF(centerX + w * 0.36f, centerY - h * 0.48f),
				new PointF(centerX - w * 0.02f, centerY - h * 0.55f),
				new PointF(centerX - w * 0.24f, centerY - h * 0.38f)
			});
		}
		else
		{
			float w = scale * 1.35f;
			float h = scale * 1.45f;
			path.AddRectangle(new RectangleF(centerX - w * 0.52f, centerY - h * 0.5f, w * 0.18f, h));
			path.AddRectangle(new RectangleF(centerX + w * 0.34f, centerY - h * 0.5f, w * 0.18f, h));
			path.AddRectangle(new RectangleF(centerX - w * 0.52f, centerY - h * 0.5f, w * 1.04f, h * 0.18f));
		}
		return path;
	}

	private static void DrawStaticObject(Graphics graphics, TrackedEntity info)
	{
		Color color = GetStaticEspColor(info.Role);
		// Do not connect part centers: distant decorations can produce oversized polygons.
		// A compact marker remains predictable at every distance.
		float scale = Math.Max(8f, Math.Min(32f, 650f / Math.Max(1f, info.Distance)));
		GraphicsPath path = CreateFallbackStaticPath(info.Role, info.ScreenX, info.ScreenY, scale);
		using (path)
		using (Brush fill = new SolidBrush(Color.FromArgb(info.Role == 6 ? 18 : 38, color)))
		using (Pen darkOutline = new Pen(Color.FromArgb(220, 8, 9, 12), 2.8f))
		using (Pen outline = new Pen(Color.FromArgb(245, color), 1.35f))
		{
			graphics.FillPath(fill, path);
			graphics.DrawPath(darkOutline, path);
			graphics.DrawPath(outline, path);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		Font font2 = new Font("Segoe UI", 10f, FontStyle.Bold);
		lock (trackedEntities)
		{
			for (int i = 0; i < trackedEntities.Count; i++)
			{
				TrackedEntity playerInfo = trackedEntities[i];
				if (!playerInfo.OnScreen)
				{
					continue;
				}
				if (playerInfo.Role >= 3 && playerInfo.Role <= 6)
				{
					if (IsStaticEspEnabled(playerInfo.Role))
					{
						DrawStaticObject(graphics, playerInfo);
					}
					continue;
				}
				float num5 = Math.Min(120f, 800f / Math.Max(1f, playerInfo.Distance));
				int num6 = (int)(num5 * 2.2f);
				int num7 = (int)(num5 * 4.5f);
				if (num6 < 2)
				{
					num6 = 2;
				}
				if (num7 < 4)
				{
					num7 = 4;
				}
				int num8 = playerInfo.ScreenX - num6 / 2;
				int num9 = playerInfo.ScreenY - (int)((float)num7 * 0.55f);
				float val = ((playerInfo.MaxHealth > 0f) ? (playerInfo.Health / playerInfo.MaxHealth) : 1f);
				val = Math.Max(0f, Math.Min(1f, val));
				int num10 = (int)(255f * (1f - val));
				int num11 = (int)(255f * val);
				Color color = Color.Lime;
				if (playerInfo.Role == 1)
				{
					color = Color.DeepSkyBlue;
				}
				else if (playerInfo.Role == 2)
				{
					if (!ShowKillerEsp)
					{
						continue;
					}
					color = Color.Red;
				}
				Pen pen2 = new Pen(Color.FromArgb(8, 9, 12), 3f);
				Pen pen3 = new Pen(color, 1.5f);
				graphics.DrawRectangle(pen2, num8, num9, num6, num7);
				graphics.DrawRectangle(pen3, num8, num9, num6, num7);
				pen3.Dispose();
				string s = $"{playerInfo.Name} [{(int)playerInfo.Distance}m]";
				SizeF sizeF = graphics.MeasureString(s, font2);
				float num12 = (float)playerInfo.ScreenX - sizeF.Width / 2f;
				float num13 = (float)num9 - sizeF.Height - 3f;
				Brush brush3 = new SolidBrush(Color.White);
				Brush brush4 = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
				graphics.DrawString(s, font2, brush4, num12 + 1f, num13 + 1f);
				graphics.DrawString(s, font2, brush3, num12, num13);
				brush3.Dispose();
				brush4.Dispose();
			}
		}
		if (DistrictClient.AutoSkillCheckEnabled)
		{
			Pen pen4 = new Pen(Color.FromArgb(100, 255, 0, 255), 2f);
			graphics.DrawRectangle(pen4, overlayWidth / 2 - 125, overlayHeight / 2 - 65, 250, 250);
			pen4.Dispose();
		}
		font2.Dispose();
	}

	internal void StartOverlay(IntPtr targetHwnd)
	{
		robloxWindow = targetHwnd;
		Application.EnableVisualStyles();
		Application.Run(this);
	}
}
