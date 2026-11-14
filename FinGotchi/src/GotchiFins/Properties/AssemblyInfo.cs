using System.Runtime.InteropServices;
using Rhino.PlugIns;

// Plug-in description attributes shown in Rhino's Plug-in Manager.
[assembly: PlugInDescription(DescriptionType.Address, "")]
[assembly: PlugInDescription(DescriptionType.Country, "")]
[assembly: PlugInDescription(DescriptionType.Email, "")]
[assembly: PlugInDescription(DescriptionType.Phone, "")]
[assembly: PlugInDescription(DescriptionType.Fax, "")]
[assembly: PlugInDescription(DescriptionType.Organization, "Raven")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "")]
[assembly: PlugInDescription(DescriptionType.WebSite, "")]

// The plugin id. Never change it once the plugin has shipped: Rhino uses it to
// identify the plugin, its settings and its registered commands.
[assembly: Guid("9e36de22-b59f-47d5-bec4-944b4269ce1e")]
