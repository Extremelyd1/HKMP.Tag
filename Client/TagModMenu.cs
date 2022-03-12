using System;
using Modding.Menu;
using Modding.Menu.Config;
using UnityEngine;
using UnityEngine.UI;

namespace HkmpTag.Client {
    /// <summary>
    /// Static class for creating and managing the mod menu.
    /// </summary>
    public static class TagModMenu {
        /// <summary>
        /// The options for number of initial infected.
        /// </summary>
        private static readonly string[] NumInfectedOptions =
            { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

        /// <summary>
        /// The currently active option of number of infected.
        /// </summary>
        private static ushort _numInfected = 1;

        /// <summary>
        /// Event that is called when the start button is pressed.
        /// </summary>
        public static event Action<ushort> StartButtonPressed;

        /// <summary>
        /// Event that is called when the end button is pressed.
        /// </summary>
        public static event Action EndButtonPressed;

        /// <summary>
        /// Create the mod menu.
        /// </summary>
        /// <param name="modListMenu">The menu screen instance.</param>
        /// <returns>A MenuBuilder instance.</returns>
        public static MenuBuilder CreateMenu(MenuScreen modListMenu) {
            void CancelAction(MenuSelectable selectable) => UIManager.instance.UIGoToDynamicMenu(modListMenu);

            return new MenuBuilder("HKMP Tag Menu")
                .CreateTitle("HKMP Tag Menu", MenuTitleStyle.vanillaStyle)
                .CreateContentPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 903f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -60f)
                    )
                ))
                .CreateControlPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 259f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -502f)
                    )
                ))
                .SetDefaultNavGraph(new GridNavGraph(1))
                .AddContent(
                    RegularGridLayout.CreateVerticalLayout(105f),
                    c => {
                        c.AddMenuButton(
                                "Start the game",
                                new MenuButtonConfig {
                                    CancelAction = CancelAction,
                                    SubmitAction = _ => StartButtonPressed?.Invoke(_numInfected),
                                    Description = new DescriptionInfo {
                                        Text = "Click to start the game"
                                    },
                                    Label = "Start the game",
                                    Proceed = false
                                })
                            .AddHorizontalOption(
                                "Number of initial infected",
                                new HorizontalOptionConfig {
                                    ApplySetting = (self, index) => {
                                        ushort.TryParse(NumInfectedOptions[index], out _numInfected);
                                    },
                                    CancelAction = CancelAction,
                                    Description = new DescriptionInfo {
                                        Text = "The number of infected chosen at the start of the game"
                                    },
                                    Label = "Number of initial infected",
                                    Options = NumInfectedOptions
                                }
                            )
                            .AddMenuButton(
                                "End the game",
                                new MenuButtonConfig {
                                    CancelAction = CancelAction,
                                    SubmitAction = _ => EndButtonPressed?.Invoke(),
                                    Description = new DescriptionInfo {
                                        Text = "Forcefully ends a game in-progress"
                                    },
                                    Label = "End the game",
                                    Proceed = false
                                });
                    })
                .AddControls(
                    new SingleContentLayout(new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -64f)
                    )), c => c.AddMenuButton(
                        "BackButton",
                        new MenuButtonConfig {
                            Label = "Back",
                            CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                            SubmitAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                            Style = MenuButtonStyle.VanillaStyle,
                            Proceed = true
                        }));
        }
    }
}
