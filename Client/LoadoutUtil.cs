namespace HkmpTag.Client {
    /// <summary>
    /// Utility class for managing charm and ability load-outs.
    /// </summary>
    public static class LoadoutUtil {
        /// <summary>
        /// Make the local player infected by (un-)equipping the appropriate items.
        /// </summary>
        public static void BecomeInfected() {
            RemoveSoul();
            SetKingSoul();
            SetCharms(26);
            SetSkills(true);
            ResetDreamGate();
            SetDreamNailUses(0);
        }

        /// <summary>
        /// Make the local player normal (non-infected) by (un-)equipping the appropriate items.
        /// </summary>
        public static void BecomeNormal() {
            RemoveSoul();
            SetKingSoul();
            SetCharms(2, 36);
            SetSkills(false);
            ResetDreamGate();
            SetDreamNailUses(2);
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
        /// Set the given indices as charms and un-equip all other charms.
        /// </summary>
        /// <param name="charmIndices">An int array containing the indices of charms to set.</param>
        private static void SetCharms(params int[] charmIndices) {
            // Un-equip all charms
            for (var i = 1; i < 40; i++) {
                PlayerData.instance.SetBool("equippedCharm_" + i, false);
                GameManager.instance.UnequipCharm(i);
            }
            
            // Equip the charms that are given as parameters
            foreach (var charmIndex in charmIndices) {
                PlayerData.instance.SetBool("equippedCharm_" + charmIndex, true);
                GameManager.instance.EquipCharm(charmIndex);
            }
            
            // Run some update methods to make sure UI doesn't glitch out
            HeroController.instance.CharmUpdate();
            GameManager.instance.RefreshOvercharm();
            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");
            PlayMakerFSM.BroadcastEvent("CHARM EQUIP CHECK");
            EventRegister.SendEvent("CHARM EQUIP CHECK");
            EventRegister.SendEvent("CHARM INDICATOR CHECK");
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

        /// <summary>
        /// Set the skills of the local player given whether they are infected or not.
        /// </summary>
        /// <param name="infected">Whether the player is infected.</param>
        private static void SetSkills(bool infected) {
            PlayerData.instance.screamLevel = 0;
            PlayerData.instance.fireballLevel = infected ? 0 : 1;
            PlayerData.instance.quakeLevel = 0;
        }

        private static void SetDreamNailUses(int uses) {
            PlayerData.instance.SetInt("dreamOrbs", uses);
        }

        private static void ResetDreamGate() {
            PlayerData.instance.SetString("dreamGateScene", "");
            PlayerData.instance.SetFloat("dreamGateX", 0f);
            PlayerData.instance.SetFloat("dreamGateY", 0f);
        }
    }
}
