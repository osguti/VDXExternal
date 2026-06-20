using System;
using System.IO;

internal static class ApplicationPaths
{
	private const string ApplicationFolderName = "ViolenceDistrictClient";
	private const string LegacyKeyPath = @"C:\key.txt";

	public static readonly string DataDirectory = BuildDataDirectory();
	public static readonly string KeyFile = Path.Combine(DataDirectory, "key.txt");
	public static readonly string SettingsFile = Path.Combine(DataDirectory, "config.json");

	public static void Initialize()
	{
		Directory.CreateDirectory(DataDirectory);
		MigrateLegacyKey();
	}

	private static string BuildDataDirectory()
	{
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		if (string.IsNullOrWhiteSpace(localAppData))
		{
			localAppData = AppDomain.CurrentDomain.BaseDirectory;
		}
		return Path.Combine(localAppData, ApplicationFolderName);
	}

	private static void MigrateLegacyKey()
	{
		try
		{
			if (!File.Exists(KeyFile) && File.Exists(LegacyKeyPath))
			{
				File.Copy(LegacyKeyPath, KeyFile, false);
			}
		}
		catch
		{
			// Authentication can still request a new key if migration fails.
		}
	}
}
