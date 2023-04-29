using System;

namespace HkmpTag.Client {
    /// <summary>
    /// Class for managing charm and ability loadouts.
    /// </summary>
    public static class LoadoutManager {
        /// <summary>
        /// Loadouts for infected and non-infected players.
        /// </summary>
        public static Loadouts Loadouts { get; set; }

        /// <summary>
        /// Make the local player infected by (un-)equipping the appropriate items.
        /// </summary>
        public static void BecomeInfected() {
            RemoveSoul();
            ResetDreamGate();
            SetKingSoul();

            if (Loadouts != null) {
                ApplyLoadout(Loadouts.InfectedLoadout);
            }
        }

        /// <summary>
        /// Make the local player normal (non-infected) by (un-)equipping the appropriate items.
        /// </summary>
        public static void BecomeNormal() {
            RemoveSoul();
            ResetDreamGate();
            SetKingSoul();

            if (Loadouts != null) {
                ApplyLoadout(Loadouts.NormalLoadout);
            }
        }

        /// <summary>
        /// Remove all soul of the local player.
        /// </summary>
        private static void RemoveSoul() {
            PlayerData.instance.ClearMP();
            GameManager.instance.soulOrb_fsm.SendEvent("MP DRAIN");
            GameManager.instance.soulVessel_fsm.SendEvent("MP RESERVE DOWN");
        }
        
        /// <summary>
        /// Reset the data for the dream gate so it can't be used to escape from a preset.
        /// </summary>
        private static void ResetDreamGate() {
            PlayerData.instance.SetString("dreamGateScene", "");
            PlayerData.instance.SetFloat("dreamGateX", 0f);
            PlayerData.instance.SetFloat("dreamGateY", 0f);
        }
        
        /// <summary>
        /// Adjust the PlayerData to make the King Soul charm available.
        /// </summary>
        private static void SetKingSoul() {
            PlayerData.instance.SetBool(nameof(PlayerData.gotCharm_36), true);
            PlayerData.instance.SetBool(nameof(PlayerData.gotShadeCharm), false);
            
            PlayerData.instance.SetInt(nameof(PlayerData.royalCharmState), 3);
            PlayerData.instance.SetInt(nameof(PlayerData.charmCost_36), 5);
        }

        private static void ApplyLoadout(Loadout loadout) {
            SetCharms(loadout.Charms);
            SetSkills(loadout.Skills);
            
            PlayerData.instance.SetInt("dreamOrbs", loadout.Essence ?? 0);
        }

        /// <summary>
        /// Set the given indices as charms and un-equip all other charms.
        /// </summary>
        /// <param name="charms">An array containing the charms to set.</param>
        private static void SetCharms(params Loadout.Charm[] charms) {
            // Un-equip all charms
            for (var i = 1; i < 40; i++) {
                PlayerData.instance.SetBool("equippedCharm_" + i, false);
                GameManager.instance.UnequipCharm(i);
            }
            
            // Equip the charms that are given as parameters
            foreach (var charm in charms) {
                var charmNum = (int) charm;
                if (charm == Loadout.Charm.Grimmchild) {
                    PlayerData.instance.grimmChildLevel = 4;
                    PlayerData.instance.destroyedNightmareLantern = false;
                } else if (charm == Loadout.Charm.CarefreeMelody) {
                    PlayerData.instance.grimmChildLevel = 5;
                    PlayerData.instance.destroyedNightmareLantern = true;
                    charmNum = 40;
                }
                
                PlayerData.instance.SetBool("equippedCharm_" + charmNum, true);
                GameManager.instance.EquipCharm(charmNum);
            }
            
            // Run some update methods to make sure UI doesn't glitch out
            HeroController.instance.CharmUpdate();
            GameManager.instance.RefreshOvercharm();
            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");
            PlayMakerFSM.BroadcastEvent("CHARM EQUIP CHECK");
            EventRegister.SendEvent("CHARM EQUIP CHECK");
            EventRegister.SendEvent("CHARM INDICATOR CHECK");
        }

        private static void SetSkills(Loadout.Skill[] skills) {
            var pd = PlayerData.instance;

            // Remove all skills
            pd.fireballLevel = 0;
            pd.quakeLevel = 0;
            pd.screamLevel = 0;
            pd.hasDash = false;
            pd.canDash = false;
            pd.hasWalljump = false;
            pd.hasSuperDash = false;
            pd.hasDoubleJump = false;
            pd.hasAcidArmour = false;
            pd.hasShadowDash = false;
            pd.hasDreamNail = false;
            pd.hasCyclone = false;
            pd.hasUpwardSlash = false;
            pd.hasDashSlash = false;

            foreach (var skill in skills) {
                switch (skill) {
                    case Loadout.Skill.VengefulSpirit:
                        pd.fireballLevel = 1;
                        break;
                    case Loadout.Skill.DesolateDive:
                        pd.quakeLevel = 1;
                        break;
                    case Loadout.Skill.HowlingWraiths:
                        pd.screamLevel = 1;
                        break;
                    case Loadout.Skill.ShadeSoul:
                        pd.fireballLevel = 2;
                        break;
                    case Loadout.Skill.DescendingDark:
                        pd.quakeLevel = 2;
                        break;
                    case Loadout.Skill.AbyssShriek:
                        pd.screamLevel = 2;
                        break;
                    case Loadout.Skill.MothwingCloak:
                        pd.hasDash = true;
                        pd.canDash = true;
                        break;
                    case Loadout.Skill.MantisClaw:
                        pd.hasWalljump = true;
                        break;
                    case Loadout.Skill.CrystalHeart:
                        pd.hasSuperDash = true;
                        break;
                    case Loadout.Skill.MonarchWings:
                        pd.hasDoubleJump = true;
                        break;
                    case Loadout.Skill.IsmasTear:
                        pd.hasAcidArmour = true;
                        break;
                    case Loadout.Skill.ShadeCloak:
                        pd.hasDash = true;
                        pd.canDash = true;
                        pd.hasShadowDash = true;
                        break;
                    case Loadout.Skill.DreamNail:
                        pd.hasDreamNail = true;
                        break;
                    case Loadout.Skill.CycloneSlash:
                        pd.hasCyclone = true;
                        break;
                    case Loadout.Skill.DashSlash:
                        pd.hasUpwardSlash = true;
                        break;
                    case Loadout.Skill.GreatSlash:
                        pd.hasDashSlash = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
