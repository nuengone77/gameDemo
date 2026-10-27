using System.Text;
using TMPro;
using UnityEngine;
using static MagicGame.ArcaneWidgets;

namespace MagicGame
{
    public partial class GameUI
    {
        // Also called for older UI prefabs, adding only the missing progression elements.
        public void BuildProgressionLayout()
        {
            var safe = transform.Find("SafeArea");
            if (safe == null) return;
            UpdateProgressionCopy(safe);
            if (safe.Find("Cards") != null) return;
            var cards = Page("Cards", safe);
            Text("CardsHeading", cards, theme, "Choose your blessing", 56, .1f, .84f, .8f, .1f, true, TextAlignmentOptions.Center);
            Text("CardsInfo", cards, theme, "", 27, .08f, .72f, .84f, .12f, false, TextAlignmentOptions.Center);
            for (int i = 0; i < 3; i++)
            {
                var button = Button("CardChoice" + i, cards, theme, "", .045f + .315f * i, .18f, .28f, .53f, i == 0);
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 28;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22;
                label.fontSizeMax = 28;
                label.margin = new Vector4(22, 16, 22, 16);
                label.overflowMode = TextOverflowModes.Overflow;
            }
            SizedButton("CardsMenu", cards, theme, "Save & Main Menu", new Vector2(.5f, .035f),
                new Vector2(.5f, 0), Vector2.zero, new Vector2(340, theme.secondaryHeight));

            var rebirth = Page("Rebirth", safe);
            Header(rebirth, "Rebirth", "Rebirth", "Permanent growth belongs to each hero. Every rebirth costs more.");
            var heroTabs = ButtonRow("RebirthHeroes", rebirth, theme);
            heroTabs.anchorMin = new Vector2(.16f, .69f);
            heroTabs.anchorMax = new Vector2(.84f, .69f);
            heroTabs.pivot = new Vector2(.5f, 0);
            for (int i = 0; i < theme.heroes.Length; i++)
                LayoutButton("RebirthHero" + i, heroTabs, theme, theme.heroes[i].displayName);
            Text("RebirthInfo", rebirth, theme, "", 29, .08f, .32f, .84f, .34f, false, TextAlignmentOptions.Center);
            var upgrades = ButtonRow("RebirthUpgrades", rebirth, theme);
            upgrades.anchorMin = new Vector2(.17f, .17f);
            upgrades.anchorMax = new Vector2(.83f, .17f);
            upgrades.pivot = new Vector2(.5f, 0);
            for (int i = 0; i < 3; i++)
                LayoutButton("RebirthUpgrade" + i, upgrades, theme, "");
            Text("RebirthUnlocks", rebirth, theme, "", 24, .06f, .025f, .88f, .11f, false, TextAlignmentOptions.Center);

            var build = Page("Build", safe);
            Header(build, "Build", "Your build", "Cards last for this run. Rebirth bonuses last between runs.");
            var content = Scroll("BuildScroll", build, .08f, .08f, .84f, .69f);
            Paragraph("BuildBody", content, theme, "", 29);

            LayoutButton("MenuRebirth", MenuContent(safe.Find("Menu/MenuPanel")), theme, "Rebirth & Records");
            LayoutButton("PauseBuild", MenuContent(safe.Find("Pause/PausePanel")), theme, "View Build");
            SizedButton("DefeatRebirth", safe.Find("Defeat"), theme, "Rebirth", new Vector2(.965f, .035f),
                new Vector2(1, 0), Vector2.zero, new Vector2(210, theme.secondaryHeight));
            Text("HUDProgression", safe.Find("HUD"), theme, "", 24, .34f, .71f, .32f, .045f, false, TextAlignmentOptions.Center);
            UpdateProgressionCopy(safe);
        }

        static void UpdateProgressionCopy(Transform safe)
        {
            foreach (var label in safe.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (label.name)
                {
                    case "PauseRestartLabel": label.text = "End Run"; break;
                    case "DefeatRetryLabel": label.text = "New Run"; break;
                    case "DefeatSubtitle": label.text = "Your cards fade. Your legacy remains."; break;
                    case "MenuFooter": label.text = "SEVEN STAGES / ENDLESS LOOPS"; break;
                    case "HelpSection3": label.text = "<color=#C8A363>04 / FOLLOW THE PORTAL</color>\nClear a stage and choose a card before entering its portal. Defeat the dragon to loop at higher difficulty. Death banks Rebirth points and removes run cards."; break;
                    case "HelpSection4": label.text = "<color=#C8A363>05 / YOUR LEGACY</color>\nContinue restores your hero, loop, build and pending card choices. Battles restart with full health. End Run banks earned points; a new adventure starts at Loop 1. Spend points on permanent, hero-specific Rebirth upgrades."; break;
                    case "DefeatStage":
                        label.rectTransform.anchorMin = new Vector2(.1f, .28f);
                        label.rectTransform.anchorMax = new Vector2(.9f, .4f);
                        break;
                    case "CardsInfo":
                        label.rectTransform.anchorMin = new Vector2(.08f, .72f);
                        label.rectTransform.anchorMax = new Vector2(.92f, .84f);
                        break;
                    case "RebirthSubtitle":
                    case "BuildSubtitle":
                    case "HUDProgression":
                    case "HUDHealth":
                    case "MoveStickCaption":
                    case "AimStickCaption":
                        label.enableAutoSizing = true;
                        break;
                }
            }
        }

        int _rebirthHero;
        void WireProgressionButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Click("CardChoice" + i, () => SelectCard(index));
                Click("RebirthUpgrade" + i, () =>
                {
                    Progression.Rebirth(theme.heroes[_rebirthHero].displayName, (RebirthStat)index);
                    RefreshRebirth();
                });
            }
            for (int i = 0; i < theme.heroes.Length; i++)
            {
                int index = i;
                Click("RebirthHero" + i, () => { _rebirthHero = index; RefreshRebirth(); });
            }
            Click("CardsMenu", () => _level.ReturnToMenu());
            Click("MenuRebirth", OpenRebirth);
            Click("DefeatRebirth", OpenRebirth);
            Click("RebirthBack", Back);
            Click("BuildBack", Back);
            Click("PauseBuild", () => { RefreshBuild(); Push(ScreenId.Build); });
        }

        void OpenRebirth()
        {
            _rebirthHero = GameSession.SelectedCharacter;
            RefreshRebirth();
            Push(ScreenId.Rebirth);
        }

        void RefreshRebirth()
        {
            string id = theme.heroes[_rebirthHero].displayName;
            var hero = Progression.Hero(id);
            var config = ProgressionConfig.Current;
            long cost = config.RebirthCost(hero.rebirths);
            bool active = Progression.HasRun && Progression.Run.hero == id;
            string record = hero.bestLoop > 0 ? $"Loop {hero.bestLoop} / Stage {hero.bestStage}" : "No stages cleared yet";
            T("RebirthInfo").text = $"<color=#66D9EA>{id}</color> / Rebirth {hero.rebirths}\nPoints: {hero.points:N0} / Next cost: {cost:N0}\n" +
                $"Permanent HP +{hero.hpBonus:P1} / Damage +{hero.damageBonus:P1} / Armor +{hero.armorBonus:0.##}\nBest: {record}\n" +
                (active ? "Finish this hero's active run before upgrading." : "Choose one permanent upgrade.");
            string[] captions = {
                $"HP +{config.rebirthHp / Mathf.Sqrt(hero.hpUpgrades + 1):P2}",
                $"Damage +{config.rebirthDamage / Mathf.Sqrt(hero.damageUpgrades + 1):P2}",
                $"Armor +{config.rebirthArmor / Mathf.Sqrt(hero.armorUpgrades + 1):0.##}"
            };
            for (int i = 0; i < 3; i++)
            {
                T("RebirthUpgrade" + i + "Label").text = captions[i];
                B("RebirthUpgrade" + i).interactable = !active && hero.points >= cost;
            }
            var unlocks = new StringBuilder("CARD UNLOCKS  ");
            foreach (var card in config.cards)
                if (card.unlockStage > 0)
                    unlocks.Append($"{card.title}: {(Progression.Data.unlocked.Contains(card.id) ? "Unlocked" : "Clear Stage " + card.unlockStage)}   ");
            T("RebirthUnlocks").text = unlocks.ToString();
            // Re-evaluate focus when buying disables an upgrade button.
            if (CurrentScreen == ScreenId.Rebirth) RefreshStack();
        }

        void RefreshProgression()
        {
            var run = Progression.Run;
            T("HUDProgression").text = run == null ? "" : $"Earned: {run.earned:N0} Rebirth points";
            if (_level != null && run != null)
                T("HUDStage").text = $"Loop {run.loop} / Stage {_level.levelNumber}";
        }

        void PresentCards()
        {
            var run = Progression.Run;
            if (_level == null || run == null || (run.phase != RunPhase.Starter && run.phase != RunPhase.Reward)) return;
            T("CardsHeading").text = run.phase == RunPhase.Starter ? "Choose your first blessing" : "Stage cleared / Choose a blessing";
            T("CardsInfo").text = $"Loop {run.loop} / Stage {run.stage} / Earned: {run.earned:N0} points\n" +
                (run.stage == 7 && run.phase == RunPhase.Reward ? "The dragon has fallen. Your build continues into a harder loop." : "Choose one. This blessing lasts until your run ends.");
            for (int i = 0; i < 3; i++)
            {
                var card = ProgressionConfig.Current.Find(run.offer[i]);
                int level = Progression.Level(run, card.id);
                string cap = card.Evergreen ? "Evergreen" : $"Max {card.maxLevel}";
                float gain = card.amount / Mathf.Sqrt(level + 1);
                string next = card.Evergreen ? (card.effect == CardEffect.Armor ? $"Next gain: +{gain:0.##} armor\n" : $"Next gain: +{gain:P1}\n") : "";
                T("CardChoice" + i + "Label").text = $"<color=#66D9EA>{card.title}</color>\n\nLevel {level} -> {level + 1} / {cap}\n\n{next}{card.description}";
            }
            Push(ScreenId.Cards);
        }

        public void SelectCard(int index)
        {
            var run = Progression.Run;
            if (CurrentScreen != ScreenId.Cards || run == null || index < 0 || index >= run.offer.Count) return;
            if (!Progression.Choose(run.offer[index])) return;
            if (_player != null && _player.ProgressionBonuses != null) _player.ProgressionBonuses.Apply();
            if (_level.portal != null) _level.portal.SetOpen(run.phase == RunPhase.Cleared);
            _stack.RemoveAt(_stack.Count - 1);
            RefreshStack();
            RefreshHealth(_player.Health);
            RefreshSkills();
            RefreshEnemies();
        }

        void RefreshBuild()
        {
            var text = new StringBuilder();
            if (_player != null)
            {
                var config = _player.Skills.Config;
                text.AppendLine($"HP {_player.Health.maxHp:0} / Armor {_player.Health.physicalDefense:0.#} physical, {_player.Health.magicDefense:0.#} magic");
                if (config != null) text.AppendLine($"Basic damage {config.basicDamage:0.#} / Attack interval {_player.Skills.AttackInterval:0.00}s\n" +
                    $"Active {config.activeDamage:0.#} damage / {config.activeCooldown:0.#}s cooldown\nUltimate {config.ultimateDamage:0.#} damage / {config.ultimateCooldown:0.#}s cooldown\n");
            }
            if (Progression.Run != null)
                foreach (var stack in Progression.Run.cards)
                {
                    var card = ProgressionConfig.Current.Find(stack.id);
                    text.AppendLine($"<color=#66D9EA>{card.title} / Level {stack.level}</color>\n{card.description}\n");
                }
            if (text.Length == 0) text.Append("No cards yet.");
            T("BuildBody").text = text.ToString();
        }
    }
}
