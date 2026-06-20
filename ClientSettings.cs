using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract]
internal sealed class ClientSettings
{
	[DataMember(Order = 1)]
	public bool PlayerEsp { get; set; }

	[DataMember(Order = 2)]
	public bool KillerEsp { get; set; }

	[DataMember(Order = 3)]
	public bool GeneratorEsp { get; set; }

	[DataMember(Order = 4)]
	public bool PalletEsp { get; set; }

	[DataMember(Order = 5)]
	public bool HookEsp { get; set; }

	[DataMember(Order = 6)]
	public bool ExitGateEsp { get; set; }

	[DataMember(Order = 7)]
	public bool Aimbot { get; set; }

	[DataMember(Order = 8)]
	public bool AutoSkillCheck { get; set; }

	[DataMember(Order = 9)]
	public bool RemoveFog { get; set; }

	[DataMember(Order = 10)]
	public string ManualKillerName { get; set; }

	public static ClientSettings CreateDefault()
	{
		return new ClientSettings
		{
			KillerEsp = true,
			ManualKillerName = string.Empty
		};
	}
}

internal static class SettingsStore
{
	public static ClientSettings Load()
	{
		ApplicationPaths.Initialize();
		if (!File.Exists(ApplicationPaths.SettingsFile))
		{
			ClientSettings defaults = ClientSettings.CreateDefault();
			Save(defaults);
			return defaults;
		}

		try
		{
			using (FileStream stream = File.OpenRead(ApplicationPaths.SettingsFile))
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ClientSettings));
				ClientSettings settings = serializer.ReadObject(stream) as ClientSettings;
				return settings ?? ClientSettings.CreateDefault();
			}
		}
		catch
		{
			return ClientSettings.CreateDefault();
		}
	}

	public static void Save(ClientSettings settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException("settings");
		}

		ApplicationPaths.Initialize();
		try
		{
			using (FileStream stream = File.Create(ApplicationPaths.SettingsFile))
			{
				DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ClientSettings));
				serializer.WriteObject(stream, settings);
			}
		}
		catch
		{
			// A persistence failure must not terminate the current session.
		}
	}
}
