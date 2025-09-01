using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace BedOwnershipTools {
    // Based off Dialog_AssignBuildingOwner and Dialog_ManageAreas and
    // Dialog_AutoSlaughter and Bill.DoInterface and Dialog_ManagePolicies
    public class Dialog_EditAssignmentGroups : Window {
        private Vector2 scrollPosition;

        // Nice to have would be to assign a colour per group
        // +---------------------------+
        // |                          X|
        // | +-----------------------+ |
        // | |^ v Default*     S R   | |
        // | |^ v Home         S R X | |
        // | |^ v Shelter      S R X | |
        // | |^ v Ship         S R X | |
        // | |                       | |
        // | +-----------------------+ |
        // |         New group         |
        // |           Close    hint   |
        // +---------------------------+

        // private const float EntryHeight = 35f;
        // private const int AssignButtonWidth = 165;
        // private const int SeparatorHeight = 7;

        // private static readonly Color DisabledColor = new Color32(55, 55, 55, 200);

        public override Vector2 InitialSize => new Vector2(550f, 400f);

        public Dialog_EditAssignmentGroups() {
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect) {
            Text.Font = GameFont.Small;
            Rect outRect = new Rect(inRect);
            outRect.yMin += 20f;
            outRect.yMax -= 40f + 35f + 7f + 7f;
            float num = 0f;
            num += (float)GameComponent_AssignmentGroupManager.Singleton.allAssignmentGroupsByPriority.Count * 24f;
            // num += 24f * 20f;
            Rect viewRect = new Rect(0f, 0f, outRect.width, num);
            Widgets.AdjustRectsForScrollView(inRect, ref outRect, ref viewRect);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < GameComponent_AssignmentGroupManager.Singleton.allAssignmentGroupsByPriority.Count; i++) {
                Rect rect = new Rect(0f, y, viewRect.width, 24f);
                DoRow(rect, GameComponent_AssignmentGroupManager.Singleton.allAssignmentGroupsByPriority[i], i);
                y += 24f;
            }
            Widgets.EndScrollView();

            Rect newAssignmentGroupButtonRect = new Rect(0f, inRect.y + inRect.height - 35f - 35f - 7f, inRect.width, 35f);
            if(Widgets.ButtonText(newAssignmentGroupButtonRect, "BedOwnershipTools.NewAssignmentGroupButton".Translate())) {
                if (GameComponent_AssignmentGroupManager.Singleton.NewAtEnd() != null) {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                } else {
                    Messages.Message("BedOwnershipTools.MaxAssignmentGroupsReached".Translate(GameComponent_AssignmentGroupManager.MAXIMUM_NONDEFAULT_GROUPS + 1), MessageTypeDefOf.RejectInput);
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
            }
            TooltipHandler.TipRegion(newAssignmentGroupButtonRect, "BedOwnershipTools.NewAssignmentGroupButtonTip".Translate());

            Rect tipRect = new Rect(inRect.x + inRect.width / 2f + Window.CloseButSize.x / 2f + 10f, inRect.y + inRect.height - 35f, 395f, 50f);
            tipRect.yMax = inRect.yMax;
            tipRect.xMax = inRect.xMax;
            Color color = GUI.color;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            if (Text.TinyFontSupported) {
                Widgets.Label(tipRect, "BedOwnershipTools.EditAssignmentGroupsTip".Translate());
            }
            else {
                Widgets.Label(tipRect, "BedOwnershipTools.EditAssignmentGroupsTip".Translate().Truncate(tipRect.width));
                TooltipHandler.TipRegion(tipRect, "BedOwnershipTools.EditAssignmentGroupsTip".Translate());
            }
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
        }

        private void DoRow(Rect rect, AssignmentGroup assignmentGroup, int i) {
            if (Mouse.IsOver(rect)) {
            	// area.MarkForDraw();
            	// GUI.color = area.Color;
            	Widgets.DrawHighlight(rect);
            	// GUI.color = Color.white;
            }
            if (i % 2 == 1) {
                Widgets.DrawLightHighlight(rect);
            }
            Widgets.BeginGroup(rect);
            WidgetRow widgetRow = new WidgetRow(0f, 0f);
            if (i > 0) {
                if (widgetRow.ButtonIcon(TexButton.ReorderUp, "BedOwnershipTools.IncreasePriorityAssignmentGroupTip".Translate(), GenUI.SubtleMouseoverColor)) {
                    GameComponent_AssignmentGroupManager.Singleton.ExchangeByIdx(i - 1, i);
			        SoundDefOf.Tick_High.PlayOneShotOnCamera();
		        }
            } else {
                if (widgetRow.ButtonIcon((Texture2D)null, null, GenUI.SubtleMouseoverColor)) {
                    // SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
                // widgetRow.Gap(24f);
            }
            if (i < GameComponent_AssignmentGroupManager.Singleton.allAssignmentGroupsByPriority.Count - 1) {
                if (widgetRow.ButtonIcon(TexButton.ReorderDown, "BedOwnershipTools.DecreasePriorityAssignmentGroupTip".Translate(), GenUI.SubtleMouseoverColor)) {
                    GameComponent_AssignmentGroupManager.Singleton.ExchangeByIdx(i, i + 1);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
		    } else {
                if (widgetRow.ButtonIcon((Texture2D)null, null, GenUI.SubtleMouseoverColor)) {
                    // SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
                // widgetRow.Gap(24f);
            }
            // Rect butRect = widgetRow.Icon(area.ColorTexture);
            // if (area is Area_Allowed area2 && Widgets.ButtonInvisible(butRect))
            // {
            //     Find.WindowStack.Add(new Dialog_AllowedAreaColorPicker(area2));
            // }
            widgetRow.Gap(4f);
            using (new TextBlock(TextAnchor.LowerLeft)) {
                // widgetRow.LabelEllipses($"{assignmentGroup.name} (ID {assignmentGroup.id})", 160f - 24f);
                if (assignmentGroup == GameComponent_AssignmentGroupManager.Singleton.defaultAssignmentGroup) {
                    widgetRow.LabelEllipses(assignmentGroup.name + "*".Colorize(Color.gray), 160f - 24f);
                } else {
                    widgetRow.LabelEllipses(assignmentGroup.name, 160f - 24f);
                }
            }
            widgetRow.Gap(60f * 3f - 24f);
            // if (widgetRow.ButtonText("ExpandArea".Translate(), null, drawBackground: true, doMouseoverSound: true, active: true, 60f))
            // {
            //     SelectDesignator<Designator_AreaAllowedExpand>(area);
            // }
            // if (widgetRow.ButtonText("ShrinkArea".Translate(), null, drawBackground: true, doMouseoverSound: true, active: true, 60f))
            // {
            //     SelectDesignator<Designator_AreaAllowedClear>(area);
            // }
            // if (widgetRow.ButtonText("InvertArea".Translate(), null, drawBackground: true, doMouseoverSound: true, active: true, 60f))
            // {
            //     area.Invert();
            // }
            widgetRow.ToggleableIcon(ref assignmentGroup.showDisplay, TexButton.SearchButton, "BedOwnershipTools.VisibilityAssignmentGroupTip".Translate());
            if (widgetRow.ButtonIcon(TexButton.Rename, "BedOwnershipTools.RenameAssignmentGroupTip".Translate(), GenUI.SubtleMouseoverColor)) {
                Find.WindowStack.Add(new Dialog_RenameAssignmentGroup(assignmentGroup));
            }
            // if (widgetRow.ButtonIcon(TexButton.Copy, null, GenUI.SubtleMouseoverColor))
            // {
            //     if (map.areaManager.TryMakeNewAllowed(out var area3))
            //     {
            //         foreach (IntVec3 activeCell in area.ActiveCells)
            //         {
            //             area3[activeCell] = true;
            //         }
            //     }
            //     else
            //     {
            //         Messages.Message("MaxAreasReached".Translate(10), MessageTypeDefOf.RejectInput);
            //     }
            // }
            if (assignmentGroup != GameComponent_AssignmentGroupManager.Singleton.defaultAssignmentGroup) {
                if (widgetRow.ButtonIcon(TexButton.Delete, "BedOwnershipTools.DeleteAssignmentGroupTip".Translate(), GenUI.SubtleMouseoverColor)) {
                    if (Input.GetKey(KeyCode.LeftControl)) {
                        GameComponent_AssignmentGroupManager.Singleton.DeleteByIdx(i);
                    } else {
                        // TODO perhaps only issue warning if group is used like
                        // RimWorld.DrugPolicyDatabase.TryDelete
                        // NOTE the game will capitalize the formatted name for some reason
                        TaggedString taggedString = "BedOwnershipTools.DeleteAssignmentGroupConfirm".Translate(assignmentGroup.name);
                        TaggedString taggedString2 = "BedOwnershipTools.DeleteAssignmentGroupConfirmButton".Translate();
                        Find.WindowStack.Add(new Dialog_Confirm(taggedString, taggedString2, delegate { GameComponent_AssignmentGroupManager.Singleton.DeleteByIdx(i); }));
                    }
                }
            } else {
                widgetRow.Gap(24f);
            }
            Widgets.EndGroup();
        }

    }
}
