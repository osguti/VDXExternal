internal sealed class TrackedEntity
{
	public string Name { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public float Health { get; set; }

	public float MaxHealth { get; set; }

	public float Distance { get; set; }

	public int ScreenX { get; set; }

	public int ScreenY { get; set; }

	public bool OnScreen { get; set; }

	public int Role { get; set; }
}
