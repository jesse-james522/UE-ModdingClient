namespace OrionLauncher.Models;

public class ServerEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> RequiredMods { get; set; } = [];
    public int MaxPlayers { get; set; }
    public string Region { get; set; } = "";

    public override string ToString() => Name;
}
