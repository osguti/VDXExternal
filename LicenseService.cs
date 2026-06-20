using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

internal static class LicenseService
{
	private const string LicenseListUrl = "https://raw.githubusercontent.com/osguti/rewrwwerwerw3/refs/heads/main/98463";
	private const int MaximumAttempts = 5;

	private static HashSet<string> validKeys;

	public static bool Authenticate()
	{
		ApplicationPaths.Initialize();
		Console.Title = "Violence District Client";
		PrintHeader();

		if (!LoadLicenseList())
		{
			WriteLine("Unable to reach the license server.", ConsoleColor.Red);
			return false;
		}

		string savedKey = ReadSavedKey();
		if (IsValid(savedKey))
		{
			WriteLine("License loaded.", ConsoleColor.Green);
			return true;
		}

		if (!string.IsNullOrWhiteSpace(savedKey))
		{
			TryDeleteSavedKey();
			WriteLine("The saved license has expired or was removed.", ConsoleColor.Yellow);
		}

		for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write("Key > ");
			Console.ResetColor();
			string key = Console.ReadLine();
			if (!IsValid(key))
			{
				WriteLine("Invalid key (" + attempt + "/" + MaximumAttempts + ").", ConsoleColor.Red);
				continue;
			}

			SaveKey(key);
			WriteLine("License accepted.", ConsoleColor.Green);
			return true;
		}

		Thread.Sleep(800);
		return false;
	}

	private static bool LoadLicenseList()
	{
		try
		{
			ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
			using (WebClient client = new WebClient())
			{
				string response = client.DownloadString(LicenseListUrl);
				validKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < lines.Length; i++)
				{
					string candidate = lines[i].Trim();
					if (candidate.Length > 0)
					{
						validKeys.Add(candidate);
					}
				}
				return validKeys.Count > 0;
			}
		}
		catch
		{
			validKeys = null;
			return false;
		}
	}

	private static bool IsValid(string key)
	{
		return validKeys != null && !string.IsNullOrWhiteSpace(key) && validKeys.Contains(key.Trim());
	}

	private static string ReadSavedKey()
	{
		try
		{
			return File.Exists(ApplicationPaths.KeyFile)
				? File.ReadAllText(ApplicationPaths.KeyFile).Trim()
				: null;
		}
		catch
		{
			return null;
		}
	}

	private static void SaveKey(string key)
	{
		try
		{
			File.WriteAllText(ApplicationPaths.KeyFile, key.Trim());
		}
		catch
		{
			WriteLine("The key was accepted but could not be saved.", ConsoleColor.Yellow);
		}
	}

	private static void TryDeleteSavedKey()
	{
		try
		{
			File.Delete(ApplicationPaths.KeyFile);
		}
		catch
		{
		}
	}

	private static void PrintHeader()
	{
		Console.Clear();
		Console.ForegroundColor = ConsoleColor.DarkMagenta;
		Console.WriteLine("Violence District Client");
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine("Local session with persistent settings");
		Console.WriteLine(new string('-', 48));
		Console.ResetColor();
	}

	private static void WriteLine(string message, ConsoleColor color)
	{
		Console.ForegroundColor = color;
		Console.WriteLine(message);
		Console.ResetColor();
	}
}
