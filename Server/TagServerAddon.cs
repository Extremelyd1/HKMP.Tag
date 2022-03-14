using Hkmp;
using Hkmp.Api.Server;

namespace HkmpTag.Server {
    /// <summary>
    /// The server addon for Tag.
    /// </summary>
    public class TagServerAddon : ServerAddon {
        /// <summary>
        /// Re-assign the logger to make it accessible.
        /// </summary>
        public new ILogger Logger => base.Logger;

        /// <inheritdoc />
        public override void Initialize(IServerApi serverApi) {
            new ServerTagManager(this, serverApi).Initialize();
        }

        /// <inheritdoc />
        protected override string Name => Identifier.Name;
        /// <inheritdoc />
        protected override string Version => Identifier.Version;
        /// <inheritdoc />
        public override bool NeedsNetwork => true;
    }
}
