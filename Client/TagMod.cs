using Hkmp.Api.Client;
using Hkmp.Api.Server;
using HkmpTag.Server;
using Modding;

namespace HkmpTag.Client {
    /// <summary>
    /// The mod class for Tag. Solely for creating a mod menu entry.
    /// </summary>
    public class TagMod : Mod, ICustomMenuMod {
        /// <inheritdoc />
        public TagMod() : base("HKMP Tag") {
        }

        /// <inheritdoc />
        public override string GetVersion() {
            return Identifier.Version;
        }

        /// <inheritdoc />
        public override void Initialize() {
            // Tell HKMP to register the client and server addons for Tag
            ClientAddon.RegisterAddon(new TagClientAddon());
            ServerAddon.RegisterAddon(new TagServerAddon());
        }

        /// <inheritdoc />
        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) =>
            TagModMenu.CreateMenu(modListMenu).Build();

        /// <inheritdoc />
        public bool ToggleButtonInsideMenu => false;
    }
}
