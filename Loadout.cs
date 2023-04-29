using Hkmp.Networking.Packet;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HkmpTag {
    /// <summary>
    /// A pair of loadouts for a game. The loadout for infected and non-infected players.
    /// </summary>
    public class Loadouts {
        /// <summary>
        /// Loadout for non-infected players.
        /// </summary>
        [JsonProperty("normal")]
        public Loadout NormalLoadout { get; }
        
        /// <summary>
        /// Loadout for infected players.
        /// </summary>
        [JsonProperty("infected")]
        public Loadout InfectedLoadout { get; }

        public Loadouts() {
            NormalLoadout = new Loadout();
            InfectedLoadout = new Loadout();
        }
    }
    
    /// <summary>
    /// A configuration of charms, skills and other player-related items that will be applied for a preset.
    /// </summary>
    public class Loadout {
        /// <summary>
        /// Byte array containing indices for charms that should be equipped.
        /// </summary>
        [JsonProperty("charms")]
        public Charm[] Charms { get; set; }

        /// <summary>
        /// Array containing the skill that should be available.
        /// </summary>
        [JsonProperty("skills")]
        public Skill[] Skills { get; set; }

        /// <summary>
        /// The number of essence that the player should have.
        /// </summary>
        [JsonProperty("essence")]
        public ushort? Essence { get; set; }

        /// <inheritdoc cref="IPacketData" />
        public void WriteData(IPacket packet) {
            var length = (byte) Charms.Length;
            packet.Write(length);

            for (var i = 0; i < length; i++) {
                packet.Write((byte) Charms[i]);
            }

            length = (byte) Skills.Length;
            packet.Write(length);

            for (var i = 0; i < length; i++) {
                packet.Write((byte) Skills[i]);
            }

            packet.Write(Essence ?? 0);
        }

        /// <inheritdoc cref="IPacketData" />
        public void ReadData(IPacket packet) {
            var length = packet.ReadByte();
            Charms = new Charm[length];

            for (var i = 0; i < length; i++) {
                Charms[i] = (Charm) packet.ReadByte();
            }

            length = packet.ReadByte();
            Skills = new Skill[length];

            for (var i = 0; i < length; i++) {
                Skills[i] = (Skill) packet.ReadByte();
            }

            Essence = packet.ReadUShort();
        }

        /// <summary>
        /// Enumeration of all charms that can be configured for a loadout.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Charm {
            GatheringSwarm = 1,
            WaywardCompass,
            GrubSong,
            StalwartShell,
            BaldurShell,
            FuryOfTheFallen,
            QuickFocus,
            LifebloodHeart,
            LifebloodCore,
            DefendersCrest,
            Flukenest,
            ThornsOfAgony,
            MarkOfPride,
            SteadyBody,
            HeavyBlow,
            SharpShadow,
            SporeShroom,
            LongNail,
            ShamanStone,
            SoulCatcher,
            SoulEater,
            GlowingWomb,
            UnbreakableHeart,
            UnbreakableGreed,
            UnbreakableStrength,
            NailmastersGlory,
            JonisBlessing,
            ShapeOfUnn,
            Hiveblood,
            Dreamwielder,
            Dashmaster,
            QuickSlash,
            SpellTwister,
            DeepFocus,
            GrubberflysElegy,
            Kingsoul,
            Sprintmaster,
            Dreamshield,
            Weaversong,
            Grimmchild,
            CarefreeMelody
        }

        /// <summary>
        /// Enumeration of all skills that can be configured for a loadout.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Skill {
            VengefulSpirit = 0,
            DesolateDive,
            HowlingWraiths,
            ShadeSoul,
            DescendingDark,
            AbyssShriek,
            MothwingCloak,
            MantisClaw,
            CrystalHeart,
            MonarchWings,
            IsmasTear,
            ShadeCloak,
            DreamNail,
            CycloneSlash,
            DashSlash,
            GreatSlash,
        }
    }
}