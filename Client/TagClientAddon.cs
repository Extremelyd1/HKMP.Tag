using Hkmp.Api.Client;
using Hkmp.Logging;

namespace HkmpTag.Client {
    /// <summary>
    /// The client addon class for Tag.
    /// </summary>
    public class TagClientAddon : TogglableClientAddon {
        /// <summary>
        /// Re-assign the logger to make it accessible.
        /// </summary>
        public new ILogger Logger => base.Logger;

        /// <summary>
        /// The tag manager for the client.
        /// </summary>
        private ClientTagManager _tagManager;

        /// <inheritdoc />
        public override void Initialize(IClientApi clientApi) {
            _tagManager = new ClientTagManager(this, clientApi);
            _tagManager.Initialize();
        }

        /// <inheritdoc />
        protected override string Name => Identifier.Name;
        /// <inheritdoc />
        protected override string Version => Identifier.Version;
        /// <inheritdoc />
        public override bool NeedsNetwork => true;

        /// <inheritdoc />
        protected override void OnEnable() {
            _tagManager.Enable();
        }

        /// <inheritdoc />
        protected override void OnDisable() {
            _tagManager.Disable();
        }
    }
}
