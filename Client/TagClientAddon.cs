using Hkmp;
using Hkmp.Api.Client;

namespace HkmpTag.Client {
    /// <summary>
    /// The client addon class for Tag.
    /// </summary>
    public class TagClientAddon : ClientAddon {
        /// <summary>
        /// Re-assign the logger to make it accessible.
        /// </summary>
        public new ILogger Logger => base.Logger;

        /// <inheritdoc />
        public override void Initialize(IClientApi clientApi) {
            new ClientTagManager(this, clientApi).Initialize();
        }

        /// <inheritdoc />
        protected override string Name => Identifier.Name;
        /// <inheritdoc />
        protected override string Version => Identifier.Version;
        /// <inheritdoc />
        public override bool NeedsNetwork => true;
    }
}
