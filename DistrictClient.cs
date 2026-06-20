using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class DistrictClient
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const uint PROCESS_VM_OPERATION_READ_WRITE_QUERY = 1080u;

	private const long VIOLENCE_DISTRICT_UNIVERSE_ID = 6739698191L;

	private const long VIOLENCE_DISTRICT_PLACE_ID = 93978595733734L;

	private const long OFF_FAKE_DM_PTR = 129824424L;

	private const long OFF_FAKE_TO_DM = 472L;

	private const long OFF_WORKSPACE = 376L;

	private const long OFF_GAME_ID = 416L;

	private const long OFF_PLACE_ID = 424L;

	private const long OFF_CHILDREN = 120L;

	private const long OFF_CLASS_DESC = 24L;

	private const long OFF_CLASS_NAME = 8L;

	private const long OFF_NAME = 176L;

	private const long OFF_LOCAL_PLAYER = 328L;

	private const long OFF_CAMERA = 1192L;

	private const long OFF_WALKSPEED = 476L;

	private const long OFF_JUMPPOWER = 432L;

	private const long OFF_GRAVITY = 528L;

	private const long OFF_HIPHEIGHT = 416L;

	private const long OFF_HEALTH = 404L;

	private const long OFF_MAXHEALTH = 436L;

	private const long OFF_FOV = 352L;

	private const long OFF_CAM_ROTATION = 248L;

	private const long OFF_LIGHTING_FOG_END = 316L;

	private const long OFF_LIGHTING_FOG_START = 320L;

	private const long OFF_ATMOSPHERE_DENSITY = 232L;

	private const long OFF_ATMOSPHERE_GLARE = 236L;

	private const long OFF_ATMOSPHERE_HAZE = 240L;

	private const long OFF_ATMOSPHERE_OFFSET = 244L;

	private const long OFF_POST_EFFECT_ENABLED = 200L;

	private static IntPtr processHandle;

	internal static IntPtr ProcessHandle { get { return processHandle; } }

	private static long baseAddress;

	internal static long BaseAddress { get { return baseAddress; } }

	private static IntPtr robloxWindow;

	private static bool espEnabled = false;

	private static Thread espThread = null;

	private static DistrictOverlay espInstance;

	private static bool isAimbotEnabled;

	internal static bool AimbotEnabled { get { return isAimbotEnabled; } }

	private static bool cframeSpeedActive = false;

	private static Thread cframeSpeedThread = null;

	private static long dataModel = 0L;

	private static long workspace = 0L;

	private static long players = 0L;

	private static long localPlayer = 0L;

	private static long humanoid = 0L;

	private static long camera = 0L;

	private static string manualKillerName = "";

	internal static string ManualKillerName { get { return manualKillerName; } }

	private static bool isAutoSkillCheckEnabled;

	internal static bool AutoSkillCheckEnabled { get { return isAutoSkillCheckEnabled; } }

	private static Thread autoSkillCheckThread = null;

	private static bool noFogEnabled = false;

	private static Thread noFogThread = null;

	private static long noFogLighting = 0L;

	private static bool hasLightingFogBackup = false;

	private static float originalFogEnd;

	private static float originalFogStart;

	private static readonly Dictionary<long, float[]> atmosphereBackups = new Dictionary<long, float[]>();

	private static readonly Dictionary<long, bool> postEffectBackups = new Dictionary<long, bool>();

	private static Process robloxProcess;

	private static Thread sessionMonitorThread;

	private static volatile bool shutdownRequested;

	private static readonly object shutdownLock = new object();

	private static ClientSettings settings;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buffer, int size, out int read);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(IntPtr proc, IntPtr addr, byte[] buffer, int size, out int written);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	private static long ReadPtr(long address)
	{
		byte[] array = new byte[8];
		ReadProcessMemory(processHandle, new IntPtr(address), array, 8, out var _);
		return BitConverter.ToInt64(array, 0);
	}

	private static float ReadFloat(long address)
	{
		byte[] array = new byte[4];
		ReadProcessMemory(processHandle, new IntPtr(address), array, 4, out var _);
		return BitConverter.ToSingle(array, 0);
	}

	private static float[] ReadFloats(long address, int count)
	{
		byte[] array = new byte[count * 4];
		ReadProcessMemory(processHandle, new IntPtr(address), array, array.Length, out var _);
		float[] array2 = new float[count];
		Buffer.BlockCopy(array, 0, array2, 0, array.Length);
		return array2;
	}

	private static void WriteFloat(long address, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		WriteProcessMemory(processHandle, new IntPtr(address), bytes, 4, out var _);
	}

	private static bool ReadBool(long address)
	{
		byte[] array = new byte[1];
		ReadProcessMemory(processHandle, new IntPtr(address), array, 1, out var _);
		return array[0] != 0;
	}

	private static void WriteBool(long address, bool value)
	{
		byte[] array = new byte[1] { value ? (byte)1 : (byte)0 };
		WriteProcessMemory(processHandle, new IntPtr(address), array, 1, out var _);
	}

	private static string ReadStr(long address, int maxLen)
	{
		byte[] array = new byte[maxLen];
		ReadProcessMemory(processHandle, new IntPtr(address), array, maxLen, out var _);
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

	private static long FindChildByClass(long parent, string className)
	{
		List<long> children = GetChildren(parent);
		for (int i = 0; i < children.Count; i++)
		{
			string className2 = GetClassName(children[i]);
			if (className2 == className)
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
			string instanceName = GetInstanceName(children[i]);
			if (instanceName == name)
			{
				return children[i];
			}
		}
		return 0L;
	}

	private static long GetDataModel()
	{
		long num = ReadPtr(baseAddress + OFF_FAKE_DM_PTR);
		if (num == 0)
		{
			return 0L;
		}
		return ReadPtr(num + 472);
	}

	private static void Scan()
	{
		dataModel = GetDataModel();
		if (dataModel == 0)
		{
			PrintError("DataModel not found.");
			return;
		}
		workspace = ReadPtr(dataModel + OFF_WORKSPACE);
		players = FindChildByClass(dataModel, "Players");
		if (players != 0)
		{
			localPlayer = ReadPtr(players + OFF_LOCAL_PLAYER);
		}
		if (localPlayer != 0L && workspace != 0)
		{
			string instanceName = GetInstanceName(localPlayer);
			if (!string.IsNullOrEmpty(instanceName))
			{
				long num = FindChildByName(workspace, instanceName);
				if (num != 0)
				{
					humanoid = FindChildByClass(num, "Humanoid");
				}
			}
		}
		if (workspace != 0)
		{
			camera = ReadPtr(workspace + OFF_CAMERA);
		}
	}

	private static void PrintStatus()
	{
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write("  DM:");
		Console.ForegroundColor = ((dataModel != 0L) ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write((dataModel != 0L) ? "OK" : "NO");
		Console.ResetColor();
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write(" | WS:");
		Console.ForegroundColor = ((workspace != 0L) ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write((workspace != 0L) ? "OK" : "NO");
		Console.ResetColor();
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write(" | PL:");
		Console.ForegroundColor = ((players != 0L) ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write((players != 0L) ? "OK" : "NO");
		Console.ResetColor();
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write(" | HUM:");
		Console.ForegroundColor = ((humanoid != 0L) ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write((humanoid != 0L) ? "OK" : "NO");
		Console.ResetColor();
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write(" | CAM:");
		Console.ForegroundColor = ((camera != 0L) ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write((camera != 0L) ? "OK" : "NO");
		Console.ResetColor();
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.Write(" | ESP:");
		Console.ForegroundColor = (espEnabled ? ConsoleColor.Green : ConsoleColor.Red);
		Console.Write(espEnabled ? "OK" : "NO");
		Console.ResetColor();
		Console.WriteLine();
	}

	private static void PrintOk(string msg)
	{
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("[OK] " + msg);
		Console.ResetColor();
	}

	private static void PrintError(string msg)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("[ERROR] " + msg);
		Console.ResetColor();
	}

	private static void PrintInfo(string msg)
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine(msg);
		Console.ResetColor();
	}

	private static bool TryAttachToViolenceDistrict(out string error)
	{
		error = "Roblox is not running.";
		Process[] candidates = Process.GetProcessesByName("RobloxPlayerBeta");
		for (int i = 0; i < candidates.Length; i++)
		{
			Process candidate = candidates[i];
			IntPtr candidateHandle = IntPtr.Zero;
			try
			{
				long candidateBaseAddress = candidate.MainModule.BaseAddress.ToInt64();
				candidateHandle = OpenProcess(PROCESS_VM_OPERATION_READ_WRITE_QUERY, false, candidate.Id);
				if (candidateHandle == IntPtr.Zero)
				{
					error = "Unable to access the Roblox process.";
					continue;
				}

				processHandle = candidateHandle;
				baseAddress = candidateBaseAddress;
				long candidateDataModel = GetDataModel();
				long universeId = candidateDataModel != 0 ? ReadPtr(candidateDataModel + OFF_GAME_ID) : 0L;
				long placeId = candidateDataModel != 0 ? ReadPtr(candidateDataModel + OFF_PLACE_ID) : 0L;
				if (universeId != VIOLENCE_DISTRICT_UNIVERSE_ID && placeId != VIOLENCE_DISTRICT_PLACE_ID)
				{
					error = candidateDataModel == 0
						? "Roblox has not loaded an experience yet."
						: "This client only works with Violence District.";
					CloseHandle(candidateHandle);
					processHandle = IntPtr.Zero;
					continue;
				}

				robloxProcess = candidate;
				robloxWindow = candidate.MainWindowHandle;
				dataModel = candidateDataModel;
				return true;
			}
			catch
			{
				if (candidateHandle != IntPtr.Zero)
				{
					CloseHandle(candidateHandle);
				}
				processHandle = IntPtr.Zero;
				error = "Unable to identify the Roblox session.";
			}
		}
		return false;
	}

	private static bool IsViolenceDistrictSessionAlive()
	{
		try
		{
			if (robloxProcess == null || robloxProcess.HasExited || processHandle == IntPtr.Zero)
			{
				return false;
			}
			long currentDataModel = GetDataModel();
			if (currentDataModel == 0)
			{
				return false;
			}
			long universeId = ReadPtr(currentDataModel + OFF_GAME_ID);
			long placeId = ReadPtr(currentDataModel + OFF_PLACE_ID);
			return universeId == VIOLENCE_DISTRICT_UNIVERSE_ID || placeId == VIOLENCE_DISTRICT_PLACE_ID;
		}
		catch
		{
			return false;
		}
	}

	private static void MonitorSession()
	{
		int invalidChecks = 0;
		while (!shutdownRequested)
		{
			if (IsViolenceDistrictSessionAlive())
			{
				invalidChecks = 0;
			}
			else if (++invalidChecks >= 3)
			{
				PrintInfo("The Violence District session has ended.");
				CleanupSession();
				Environment.Exit(0);
				return;
			}
			Thread.Sleep(1000);
		}
	}

	private static void SaveCurrentSettings()
	{
		if (settings == null)
		{
			settings = ClientSettings.CreateDefault();
		}
		settings.PlayerEsp = espEnabled;
		settings.KillerEsp = DistrictOverlay.ShowKillerEsp;
		settings.GeneratorEsp = DistrictOverlay.ShowGeneratorEsp;
		settings.PalletEsp = DistrictOverlay.ShowPalletEsp;
		settings.HookEsp = DistrictOverlay.ShowHookEsp;
		settings.ExitGateEsp = DistrictOverlay.ShowExitGateEsp;
		settings.Aimbot = isAimbotEnabled;
		settings.AutoSkillCheck = isAutoSkillCheckEnabled;
		settings.RemoveFog = noFogEnabled;
		settings.ManualKillerName = manualKillerName ?? string.Empty;
		SettingsStore.Save(settings);
	}

	private static void LoadSettings()
	{
		settings = SettingsStore.Load();
		DistrictOverlay.ShowKillerEsp = settings.KillerEsp;
		DistrictOverlay.ShowGeneratorEsp = settings.GeneratorEsp;
		DistrictOverlay.ShowPalletEsp = settings.PalletEsp;
		DistrictOverlay.ShowHookEsp = settings.HookEsp;
		DistrictOverlay.ShowExitGateEsp = settings.ExitGateEsp;
		isAimbotEnabled = settings.Aimbot;
		manualKillerName = settings.ManualKillerName ?? string.Empty;
	}

	private static void SetOverlayEnabled(bool enabled)
	{
		if (enabled == espEnabled)
		{
			return;
		}
		espEnabled = enabled;
		if (enabled)
		{
			espThread = new Thread((ThreadStart)delegate
			{
				espInstance = new DistrictOverlay();
				espInstance.StartOverlay(robloxWindow);
			});
			espThread.SetApartmentState(ApartmentState.STA);
			espThread.IsBackground = true;
			espThread.Start();
		}
		else if (espInstance != null && !espInstance.IsDisposed)
		{
			try
			{
				espInstance.BeginInvoke((MethodInvoker)delegate { espInstance.Close(); });
			}
			catch
			{
			}
			espInstance = null;
		}
	}

	private static void SetAutoSkillCheckEnabled(bool enabled)
	{
		isAutoSkillCheckEnabled = enabled;
		if (enabled && (autoSkillCheckThread == null || !autoSkillCheckThread.IsAlive))
		{
			autoSkillCheckThread = new Thread(AutoSkillCheckLoop);
			autoSkillCheckThread.IsBackground = true;
			autoSkillCheckThread.Start();
		}
	}

	private static void SetNoFogEnabled(bool enabled)
	{
		noFogEnabled = enabled;
		if (enabled && (noFogThread == null || !noFogThread.IsAlive))
		{
			noFogThread = new Thread(NoFogLoop);
			noFogThread.IsBackground = true;
			noFogThread.Start();
		}
		else if (!enabled && noFogThread != null && noFogThread.IsAlive)
		{
			noFogThread.Join(300);
		}
	}

	private static void CleanupSession()
	{
		lock (shutdownLock)
		{
			if (shutdownRequested)
			{
				return;
			}
			shutdownRequested = true;
			SaveCurrentSettings();
			cframeSpeedActive = false;
			isAutoSkillCheckEnabled = false;
			noFogEnabled = false;
			isAimbotEnabled = false;
			espEnabled = false;
			if (espInstance != null && !espInstance.IsDisposed)
			{
				try
				{
					espInstance.BeginInvoke((MethodInvoker)delegate { espInstance.Close(); });
				}
				catch
				{
				}
			}
			if (noFogThread != null && noFogThread.IsAlive && Thread.CurrentThread != noFogThread)
			{
				noFogThread.Join(300);
			}
			RestoreNoFogState();
			if (processHandle != IntPtr.Zero)
			{
				CloseHandle(processHandle);
				processHandle = IntPtr.Zero;
			}
		}
	}

	private static void Main(string[] args)
	{
		ApplicationPaths.Initialize();
		LoadSettings();
		if (!LicenseService.Authenticate())
		{
			return;
		}

		string attachError;
		if (!TryAttachToViolenceDistrict(out attachError))
		{
			PrintError(attachError);
			Console.ReadKey();
			return;
		}

		SetOverlayEnabled(settings.PlayerEsp);
		SetAutoSkillCheckEnabled(settings.AutoSkillCheck);
		SetNoFogEnabled(settings.RemoveFog);
		sessionMonitorThread = new Thread(MonitorSession);
		sessionMonitorThread.IsBackground = true;
		sessionMonitorThread.Start();

		PrintInfo($"[INFO] Violence District - PID: {robloxProcess.Id}");
		Console.WriteLine();
		bool flag = true;
		while (flag)
		{
			Console.Clear();
			PrintInfo($"[INFO] Violence District - PID: {robloxProcess.Id}");
			PrintInfo($"[INFO] Config: {ApplicationPaths.SettingsFile}");
			Console.WriteLine();
			Scan();
			if (players != 0)
			{
				PrintOk($"Players: 0x{players:X}");
			}
			if (localPlayer != 0)
			{
				PrintOk("LocalPlayer:");
			}
			if (camera != 0)
			{
				PrintOk($"Camera: 0x{camera:X}");
			}
			if (players != 0L || localPlayer != 0L || camera != 0)
			{
				Console.WriteLine();
			}
			PrintStatus();
			PrintInfo("======== MENU ========");
			Console.WriteLine("[1] Configure CFrame movement");
			Console.WriteLine("[2] Set JumpPower");
			Console.WriteLine("[3] Set Gravity");
			Console.WriteLine("[4] Set HipHeight");
			Console.WriteLine("[5] God Mode");
			Console.WriteLine("[6] Set FOV");
			Console.WriteLine("[7] Show Current Values");
			Console.WriteLine(espEnabled ? "[8] Disable Player ESP" : "[8] Enable Player ESP");
			Console.WriteLine(isAimbotEnabled ? "[9] Disable Aimbot" : "[9] Enable Aimbot");
			Console.WriteLine("[10] Fling Player");
			Console.WriteLine(DistrictOverlay.ShowKillerEsp ? "[11] Disable Killer ESP" : "[11] Enable Killer ESP");
			Console.WriteLine(DistrictOverlay.ShowGeneratorEsp ? "[12] Disable Generator ESP" : "[12] Enable Generator ESP");
			Console.WriteLine(DistrictOverlay.ShowPalletEsp ? "[13] Disable Pallet ESP" : "[13] Enable Pallet ESP");
			Console.WriteLine("[14] Set Target Killer (Name)");
			Console.WriteLine(isAutoSkillCheckEnabled ? "[15] Disable Auto Skill Check" : "[15] Enable Auto Skill Check");
			Console.WriteLine(DistrictOverlay.ShowHookEsp ? "[16] Disable Hook ESP" : "[16] Enable Hook ESP");
			Console.WriteLine(DistrictOverlay.ShowExitGateEsp ? "[17] Disable Exit/Gate ESP" : "[17] Enable Exit/Gate ESP");
			Console.WriteLine(noFogEnabled ? "[18] Disable Remove Fog" : "[18] Enable Remove Fog");
			Console.WriteLine("[0] Exit");
			Console.Write("\n > ");
			string text = Console.ReadLine();
			if (text != null)
			{
				text = text.Trim();
			}
			Scan();
			switch (text)
			{
			case "1":
			{
				if (localPlayer == 0)
				{
					PrintError("LocalPlayer not found.");
					break;
				}
				Console.Write("CFrame Speedhack Multiplier (default 0.5): ");
				if (float.TryParse(Console.ReadLine(), out var speedMult))
				{
					if (cframeSpeedThread != null)
					{
						cframeSpeedActive = false;
						Thread.Sleep(100);
					}
					cframeSpeedActive = true;
					cframeSpeedThread = new Thread((ThreadStart)delegate
					{
						while (cframeSpeedActive)
						{
							short asyncKeyState = GetAsyncKeyState(87);
							if ((asyncKeyState & 0x8000) != 0 && camera != 0L && localPlayer != 0)
							{
								long num24 = FindChildByName(workspace, GetInstanceName(localPlayer));
								if (num24 != 0)
								{
									long num25 = FindChildByName(num24, "HumanoidRootPart");
									if (num25 == 0)
									{
										num25 = FindChildByName(num24, "Torso");
									}
									if (num25 != 0)
									{
										long num26 = ReadPtr(num25 + 328);
										if (num26 > 1099511627776L && num26 < 140737488355327L)
										{
										float[] array = ReadFloats(camera + OFF_CAM_ROTATION, 9);
											float num27 = 0f - array[2];
											float num28 = 0f - array[8];
											float num29 = ReadFloat(num26 + 308);
											float num30 = ReadFloat(num26 + 316);
											WriteFloat(num26 + 308, num29 + num27 * speedMult);
											WriteFloat(num26 + 316, num30 + num28 * speedMult);
										}
									}
								}
							}
							Thread.Sleep(10);
						}
					});
					cframeSpeedThread.IsBackground = true;
					cframeSpeedThread.Start();
					PrintOk("CFrame movement enabled. Hold W to move.");
				}
				else
				{
					cframeSpeedActive = false;
					PrintOk("CFrame Speedhack Disabled.");
				}
				break;
			}
			case "2":
			{
				if (humanoid == 0)
				{
					PrintError("Humanoid not found.");
					break;
				}
				Console.Write("JumpPower (default 50): ");
				if (float.TryParse(Console.ReadLine(), out var result2))
				{
					WriteFloat(humanoid + OFF_JUMPPOWER, result2);
					PrintOk($"JumpPower = {result2}");
				}
				break;
			}
			case "3":
			{
				if (workspace == 0)
				{
					PrintError("Workspace not found.");
					break;
				}
				Console.Write("Gravity (default 196.2): ");
				if (float.TryParse(Console.ReadLine(), out var result5))
				{
					WriteFloat(workspace + OFF_GRAVITY, result5);
					PrintOk($"Gravity = {result5}");
				}
				break;
			}
			case "4":
			{
				if (humanoid == 0)
				{
					PrintError("Humanoid not found.");
					break;
				}
				Console.Write("HipHeight (default 2): ");
				if (float.TryParse(Console.ReadLine(), out var result4))
				{
					WriteFloat(humanoid + OFF_HIPHEIGHT, result4);
					PrintOk($"HipHeight = {result4}");
				}
				break;
			}
			case "5":
				if (humanoid == 0)
				{
					PrintError("Humanoid not found.");
					break;
				}
				WriteFloat(humanoid + OFF_HEALTH, 999999f);
				WriteFloat(humanoid + OFF_MAXHEALTH, 999999f);
				PrintOk("God Mode ON (Health = 999999)");
				break;
			case "6":
			{
				if (camera == 0)
				{
					PrintError("Camera not found.");
					break;
				}
				Console.Write("FOV in degrees (default 70): ");
				if (float.TryParse(Console.ReadLine(), out var result))
				{
					float value = result * ((float)Math.PI / 180f);
					WriteFloat(camera + OFF_FOV, value);
					PrintOk($"FOV = {result} degrees");
				}
				break;
			}
			case "7":
				Console.WriteLine();
				if (humanoid != 0)
				{
					float num17 = ReadFloat(humanoid + OFF_WALKSPEED);
					float num18 = ReadFloat(humanoid + OFF_JUMPPOWER);
					float num19 = ReadFloat(humanoid + OFF_HIPHEIGHT);
					float num20 = ReadFloat(humanoid + OFF_HEALTH);
					float num21 = ReadFloat(humanoid + OFF_MAXHEALTH);
					PrintInfo($"  WalkSpeed:  {num17}");
					PrintInfo($"  JumpPower:  {num18}");
					PrintInfo($"  HipHeight:  {num19}");
					PrintInfo($"  Health:     {num20} / {num21}");
				}
				else
				{
					PrintError("Humanoid not found.");
				}
				if (workspace != 0)
				{
					PrintInfo($"  Gravity:    {ReadFloat(workspace + OFF_GRAVITY)}");
				}
				if (camera != 0)
				{
					float num22 = ReadFloat(camera + OFF_FOV);
					float num23 = num22 * 57.29578f;
					PrintInfo($"  FOV:        {num23} degrees");
				}
				break;
			case "8":
				SetOverlayEnabled(!espEnabled);
				PrintOk(espEnabled ? "ESP Enabled." : "ESP Disabled.");
				break;
			case "9":
				isAimbotEnabled = !isAimbotEnabled;
				if (isAimbotEnabled)
				{
					PrintOk("Aimbot enabled. Hold right mouse button to track the killer.");
				}
				else
				{
					PrintOk("Aimbot Disabled.");
				}
				break;
			case "10":
			{
				if (workspace == 0)
				{
					PrintError("Workspace not found.");
					break;
				}
				List<long> children = GetChildren(workspace);
				List<long> list = new List<long>();
				List<long> list2 = new List<long>();
				List<string> list3 = new List<string>();
				List<int> list4 = new List<int>();
				long num = 0L;
				for (int num2 = 0; num2 < children.Count; num2++)
				{
					long num3 = children[num2];
					if (GetClassName(num3) != "Model")
					{
						continue;
					}
					long num4 = FindChildByClass(num3, "Humanoid");
					if (num4 == 0)
					{
						continue;
					}
					long num5 = 0L;
					List<long> children2 = GetChildren(num3);
					for (int num6 = 0; num6 < children2.Count; num6++)
					{
						string instanceName = GetInstanceName(children2[num6]);
						if (instanceName.Contains("HumanoidRootPart") || instanceName.Contains("Torso"))
						{
							num5 = children2[num6];
							break;
						}
					}
					if (num5 == 0)
					{
						continue;
					}
					string text2 = GetInstanceName(num3);
					if (string.IsNullOrEmpty(text2) || text2 == "?" || text2 == "Unknown")
					{
						text2 = "Player_" + list.Count;
					}
					if (localPlayer != 0)
					{
						string instanceName2 = GetInstanceName(localPlayer);
						if (text2 == instanceName2)
						{
							num = ReadPtr(num5 + 328);
							continue;
						}
					}
					int item = 0;
					long num7 = FindChildByName(players, text2);
					if (num7 != 0)
					{
						long num8 = FindChildByName(num7, "Backpack");
						if (num8 != 0)
						{
							List<long> children3 = GetChildren(num8);
							for (int num9 = 0; num9 < children3.Count; num9++)
							{
								if (!(GetClassName(children3[num9]) == "Tool"))
								{
									continue;
								}
								string text3 = GetInstanceName(children3[num9]).ToLower();
								if (text3 == "gun" || text3 == "revolver")
								{
									item = 1;
								}
								if (text3 == "knife" || text3 == "dagger")
								{
									item = 2;
								}
								List<long> children4 = GetChildren(children3[num9]);
								for (int num10 = 0; num10 < children4.Count; num10++)
								{
									string text4 = GetInstanceName(children4[num10]).ToLower();
									if (text4.Contains("gunscript") || text4.Contains("gunstates") || text4 == "gun")
									{
										item = 1;
									}
									if (text4.Contains("knifeserver") || text4.Contains("knifeclient") || text4.Contains("knifescript") || text4 == "knife")
									{
										item = 2;
									}
								}
							}
						}
					}
					for (int num11 = 0; num11 < children2.Count; num11++)
					{
						if (!(GetClassName(children2[num11]) == "Tool"))
						{
							continue;
						}
						string text5 = GetInstanceName(children2[num11]).ToLower();
						if (text5 == "gun" || text5 == "revolver")
						{
							item = 1;
						}
						if (text5 == "knife" || text5 == "dagger")
						{
							item = 2;
						}
						List<long> children5 = GetChildren(children2[num11]);
						for (int num12 = 0; num12 < children5.Count; num12++)
						{
							string text6 = GetInstanceName(children5[num12]).ToLower();
							if (text6.Contains("gunscript") || text6.Contains("gunstates") || text6 == "gun")
							{
								item = 1;
							}
							if (text6.Contains("knifeserver") || text6.Contains("knifeclient") || text6.Contains("knifescript") || text6 == "knife")
							{
								item = 2;
							}
						}
					}
					list.Add(num5);
					list2.Add(num4);
					list3.Add(text2);
					list4.Add(item);
				}
				if (list.Count == 0)
				{
					PrintError("No players found.");
					Thread.Sleep(1500);
					break;
				}
				Console.WriteLine("\n--- SELECT PLAYER ---");
				for (int num13 = 0; num13 < list3.Count; num13++)
				{
					string text7 = "";
					ConsoleColor consoleColor = ConsoleColor.Green;
					if (list4[num13] == 1)
					{
						text7 = " (SHERIFF)";
						consoleColor = ConsoleColor.Cyan;
					}
					else if (list4[num13] == 2)
					{
						text7 = " (KILLER)";
						consoleColor = ConsoleColor.Red;
					}
					else
					{
						text7 = " (Innocent)";
						consoleColor = ConsoleColor.Green;
					}
					Console.ForegroundColor = ConsoleColor.White;
					Console.Write($"[{num13 + 1}] ");
					Console.ForegroundColor = consoleColor;
					Console.WriteLine(list3[num13] + text7);
				}
				Console.ResetColor();
				Console.Write("\nTarget Number: ");
				string s = Console.ReadLine();
				if (int.TryParse(s, out var result3) && result3 >= 1 && result3 <= list3.Count)
				{
					long num14 = list[result3 - 1];
					long num15 = list2[result3 - 1];
					long num16 = ReadPtr(num14 + 328);
					string text8 = list3[result3 - 1];
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("\n--- ACTION FOR " + text8 + " ---");
					Console.ResetColor();
					Console.WriteLine("[1] Fling (send out of map)");
					Console.WriteLine("[2] Kill (set HP to 0)");
					Console.WriteLine("[3] Kill + fling");
					Console.Write("\nAction: ");
					string text9 = Console.ReadLine();
					bool flag2 = text9 == "1" || text9 == "3";
					bool flag3 = text9 == "2" || text9 == "3";
					if (!flag2 && !flag3)
					{
						PrintError("Invalid action.");
						Thread.Sleep(1500);
						break;
					}
					if (flag3)
					{
						long killHum = num15;
						long fPrim = num16;
						Thread thread = new Thread((ThreadStart)delegate
						{
							while (true)
							{
								WriteFloat(killHum + OFF_HEALTH, 0f);
								WriteFloat(fPrim + 308, float.NaN);
								WriteFloat(fPrim + 312, float.NaN);
								WriteFloat(fPrim + 316, float.NaN);
								Thread.Sleep(10);
							}
						});
						thread.IsBackground = true;
						thread.Start();
					}
					if (flag2)
					{
						if (num16 == 0L || num16 < 1099511627776L || num16 > 140737488355327L)
						{
							PrintError("Could not get target Primitive for fling.");
							Thread.Sleep(2000);
							break;
						}
						if (num == 0L || num < 1099511627776L || num > 140737488355327L)
						{
							PrintError("Could not get YOUR Primitive for physics fling.");
							Thread.Sleep(2000);
							break;
						}
						long fPrim2 = num16;
						long mPrim = num;
						Thread thread2 = new Thread((ThreadStart)delegate
						{
							float value2 = ReadFloat(mPrim + 308);
							float value3 = ReadFloat(mPrim + 312);
							float value4 = ReadFloat(mPrim + 316);
							for (int i = 0; i < 25; i++)
							{
								float value5 = ReadFloat(fPrim2 + 308);
								float value6 = ReadFloat(fPrim2 + 312);
								float value7 = ReadFloat(fPrim2 + 316);
								WriteFloat(mPrim + 308, value5);
								WriteFloat(mPrim + 312, value6);
								WriteFloat(mPrim + 316, value7);
								Thread.Sleep(20);
							}
							WriteFloat(mPrim + 308, value2);
							WriteFloat(mPrim + 312, value3);
							WriteFloat(mPrim + 316, value4);
						});
						thread2.IsBackground = true;
						thread2.Start();
					}
					string text10 = ((text9 == "1") ? "flung" : ((text9 == "2") ? "killed" : "killed and flung"));
					PrintOk(text8 + " was " + text10 + ".");
					Thread.Sleep(1500);
				}
				else
				{
					PrintError("Invalid selection.");
					Thread.Sleep(2000);
				}
				break;
			}
			case "11":
				DistrictOverlay.ShowKillerEsp = !DistrictOverlay.ShowKillerEsp;
				PrintOk(DistrictOverlay.ShowKillerEsp ? "Killer ESP Enabled." : "Killer ESP Disabled.");
				break;
			case "12":
				DistrictOverlay.ShowGeneratorEsp = !DistrictOverlay.ShowGeneratorEsp;
				PrintOk(DistrictOverlay.ShowGeneratorEsp ? "Generator ESP Enabled." : "Generator ESP Disabled.");
				break;
			case "13":
				DistrictOverlay.ShowPalletEsp = !DistrictOverlay.ShowPalletEsp;
				PrintOk(DistrictOverlay.ShowPalletEsp ? "Pallet ESP Enabled." : "Pallet ESP Disabled.");
				break;
			case "14":
				Console.Write("Enter Killer's Exact Name (or leave empty to reset): ");
				manualKillerName = (Console.ReadLine() ?? string.Empty).Trim();
				PrintOk(string.IsNullOrEmpty(manualKillerName) ? "Target Killer reset." : ("Target Killer set to: " + manualKillerName));
				break;
			case "15":
				SetAutoSkillCheckEnabled(!isAutoSkillCheckEnabled);
				PrintOk(isAutoSkillCheckEnabled ? "Auto Skill Check Enabled." : "Auto Skill Check Disabled.");
				break;
			case "16":
				DistrictOverlay.ShowHookEsp = !DistrictOverlay.ShowHookEsp;
				PrintOk(DistrictOverlay.ShowHookEsp ? "Hook ESP Enabled." : "Hook ESP Disabled.");
				break;
			case "17":
				DistrictOverlay.ShowExitGateEsp = !DistrictOverlay.ShowExitGateEsp;
				PrintOk(DistrictOverlay.ShowExitGateEsp ? "Exit/Gate ESP Enabled." : "Exit/Gate ESP Disabled.");
				break;
			case "18":
				SetNoFogEnabled(!noFogEnabled);
				PrintOk(noFogEnabled ? "Remove Fog Enabled." : "Remove Fog Disabled. Lighting restored.");
				break;
			case "0":
				flag = false;
				break;
			}
			SaveCurrentSettings();
		}
		CleanupSession();
	}

	private static bool IsPostProcessingEffect(string className)
	{
		return className == "BloomEffect" || className == "BlurEffect" ||
			className == "DepthOfFieldEffect" || className == "SunRaysEffect" ||
			className == "ColorCorrectionEffect" || className == "ColorGradingEffect";
	}

	private static void RestoreNoFogState()
	{
		try
		{
			if (noFogLighting != 0 && hasLightingFogBackup)
			{
				WriteFloat(noFogLighting + OFF_LIGHTING_FOG_END, originalFogEnd);
				WriteFloat(noFogLighting + OFF_LIGHTING_FOG_START, originalFogStart);
			}
			foreach (KeyValuePair<long, float[]> backup in atmosphereBackups)
			{
				WriteFloat(backup.Key + OFF_ATMOSPHERE_DENSITY, backup.Value[0]);
				WriteFloat(backup.Key + OFF_ATMOSPHERE_GLARE, backup.Value[1]);
				WriteFloat(backup.Key + OFF_ATMOSPHERE_HAZE, backup.Value[2]);
				WriteFloat(backup.Key + OFF_ATMOSPHERE_OFFSET, backup.Value[3]);
			}
			foreach (KeyValuePair<long, bool> backup2 in postEffectBackups)
			{
				WriteBool(backup2.Key + OFF_POST_EFFECT_ENABLED, backup2.Value);
			}
		}
		catch
		{
		}
		noFogLighting = 0L;
		hasLightingFogBackup = false;
		atmosphereBackups.Clear();
		postEffectBackups.Clear();
	}

	private static void ApplyNoFog(long lighting)
	{
		if (lighting == 0)
		{
			return;
		}
		if (lighting != noFogLighting)
		{
			RestoreNoFogState();
			noFogLighting = lighting;
			originalFogEnd = ReadFloat(lighting + OFF_LIGHTING_FOG_END);
			originalFogStart = ReadFloat(lighting + OFF_LIGHTING_FOG_START);
			hasLightingFogBackup = true;
		}
		WriteFloat(lighting + OFF_LIGHTING_FOG_END, 1000000f);
		WriteFloat(lighting + OFF_LIGHTING_FOG_START, 0f);
		List<long> children = GetChildren(lighting);
		for (int i = 0; i < children.Count; i++)
		{
			long instance = children[i];
			string className = GetClassName(instance);
			if (className == "Atmosphere")
			{
				if (!atmosphereBackups.ContainsKey(instance))
				{
					atmosphereBackups[instance] = new float[4]
					{
						ReadFloat(instance + OFF_ATMOSPHERE_DENSITY),
						ReadFloat(instance + OFF_ATMOSPHERE_GLARE),
						ReadFloat(instance + OFF_ATMOSPHERE_HAZE),
						ReadFloat(instance + OFF_ATMOSPHERE_OFFSET)
					};
				}
				WriteFloat(instance + OFF_ATMOSPHERE_DENSITY, 0f);
				WriteFloat(instance + OFF_ATMOSPHERE_GLARE, 0f);
				WriteFloat(instance + OFF_ATMOSPHERE_HAZE, 0f);
				WriteFloat(instance + OFF_ATMOSPHERE_OFFSET, 0f);
			}
			else if (IsPostProcessingEffect(className))
			{
				if (!postEffectBackups.ContainsKey(instance))
				{
					postEffectBackups[instance] = ReadBool(instance + OFF_POST_EFFECT_ENABLED);
				}
				WriteBool(instance + OFF_POST_EFFECT_ENABLED, false);
			}
		}
	}

	private static void NoFogLoop()
	{
		try
		{
			while (noFogEnabled)
			{
				long fakeDataModel = ReadPtr(baseAddress + OFF_FAKE_DM_PTR);
				long currentDataModel = fakeDataModel != 0 ? ReadPtr(fakeDataModel + OFF_FAKE_TO_DM) : 0L;
				long lighting = currentDataModel != 0 ? FindChildByClass(currentDataModel, "Lighting") : 0L;
				ApplyNoFog(lighting);
				Thread.Sleep(50);
			}
		}
		catch
		{
		}
		finally
		{
			RestoreNoFogState();
		}
	}

	private static void AutoSkillCheckLoop()
	{
		Bitmap bitmap = new Bitmap(250, 250);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			while (isAutoSkillCheckEnabled)
			{
				try
				{
					if (robloxWindow != IntPtr.Zero && GetWindowRect(robloxWindow, out var rect))
					{
						int num = rect.Right - rect.Left;
						int num2 = rect.Bottom - rect.Top;
						int num3 = rect.Left + num / 2;
						int num4 = rect.Top + num2 / 2;
						Rectangle rectangle = new Rectangle(num3 - 125, num4 - 65, 250, 250);
						graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
						bool flag = false;
						for (int i = 0; i < 250; i += 2)
						{
							for (int j = 0; j < 250; j += 2)
							{
								Color pixel = bitmap.GetPixel(j, i);
								if (pixel.R > 230 && pixel.G > 230 && pixel.B > 230)
								{
									flag = true;
									break;
								}
							}
							if (flag)
							{
								break;
							}
						}
						if (flag)
						{
							keybd_event(32, 0, 0u, UIntPtr.Zero);
							Thread.Sleep(50);
							keybd_event(32, 0, 2u, UIntPtr.Zero);
							Thread.Sleep(800);
						}
					}
				}
				catch
				{
				}
				Thread.Sleep(15);
			}
		}
		bitmap.Dispose();
	}
}
