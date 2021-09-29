﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using Hitbloq.Entries;
using Hitbloq.Interfaces;
using Hitbloq.Sources;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Zenject;

namespace Hitbloq.UI
{
    [HotReload(RelativePathToLayout = @"..\Views\HitbloqPanel.bsml")]
    [ViewDefinition("Hitbloq.UI.Views.HitbloqPanel.bsml")]
    internal class HitbloqPanelController : BSMLAutomaticViewController, INotifyUserRegistered, IDifficultyBeatmapUpdater, IPoolUpdater, ILeaderboardEntriesUpdater
    {
        private HitbloqFlowCoordinator hitbloqFlowCoordinator;
        private IVRPlatformHelper platformHelper;
        private RankInfoSource rankInfoSource;
        private PoolInfoSource poolInfoSource;

        private HitbloqRankInfo rankInfo;
        private List<string> poolNames;
        private bool _cuteMode;
        private string _promptText;
        private bool _loadingActive;

        private Sprite logoSprite;
        private Sprite flushedSprite;

        private CancellationTokenSource poolInfoTokenSource;
        private CancellationTokenSource rankInfoTokenSource;

        public event Action<string> PoolChangedEvent;
        public event Action<HitbloqRankInfo, string> ClickedRankText;

        private bool CuteMode
        {
            get => _cuteMode;
            set
            {
                if (_cuteMode != value)
                {
                    if (flushedSprite == null)
                    {
                        flushedSprite = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("Hitbloq.Images.LogoFlushed.png");
                    }

                    if (logo != null)
                    {
                        logo.sprite = value ? flushedSprite : logoSprite;
                        logo.GetComponent<HoverHint>().enabled = value;
                    }
                }
                _cuteMode = value;
            }
        }

        [UIComponent("container")]
        private readonly Backgroundable container;

        [UIComponent("hitbloq-logo")]
        private readonly ImageView logo;

        [UIComponent("separator")]
        private readonly ImageView separator;

        [UIComponent("dropdown-list")]
        private readonly DropDownListSetting dropDownListSetting;

        [UIComponent("dropdown-list")]
        private readonly RectTransform dropDownListTransform;

        [Inject]
        private void Inject(HitbloqFlowCoordinator hitbloqFlowCoordinator, IVRPlatformHelper platformHelper, RankInfoSource rankInfoSource, PoolInfoSource poolInfoSource)
        {
            this.hitbloqFlowCoordinator = hitbloqFlowCoordinator;
            this.platformHelper = platformHelper;
            this.rankInfoSource = rankInfoSource;
            this.poolInfoSource = poolInfoSource;
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            container.background.material = BeatSaberMarkupLanguage.Utilities.ImageResources.NoGlowMat;
            ImageView background = container.background as ImageView;
            background.color0 = Color.white;
            background.color1 = new Color(1f, 1f, 1f, 0f);
            background.color = Color.gray;
            background.SetField("_gradient", true);
            background.SetField("_skew", 0.18f);

            logoSprite = logo.sprite;
            logo.sprite = CuteMode ? flushedSprite : logoSprite;
            logo.GetComponent<HoverHint>().enabled = CuteMode;
            
            logo.SetField("_skew", 0.18f);
            logo.SetVerticesDirty();

            separator.SetVerticesDirty();
            separator.SetField("_skew", 0.18f);

            CurvedTextMeshPro dropdownText = dropDownListTransform.GetComponentInChildren<CurvedTextMeshPro>();
            dropdownText.fontSize = 3.5f;
            dropdownText.transform.localPosition = new Vector3(-1.5f, 0, 0);

            (dropDownListSetting.dropdown as DropdownWithTableView).SetField("_numberOfVisibleCells", 2);
            dropDownListSetting.values = new List<object>() { "1", "2" };
            dropDownListSetting.UpdateChoices();
            dropDownListSetting.dropdown.SelectCellWithIdx(0);
            dropDownListSetting.values = pools.Count != 0 ? pools : new List<object> { "None" };
            dropDownListSetting.UpdateChoices();

            dropDownListSetting.GetComponentInChildren<ScrollView>(true).SetField("_platformHelper", platformHelper);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            dropDownListSetting.dropdown.Hide(false);
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        [UIAction("pool-changed")]
        private void PoolChanged(string formattedPool)
        {
            PoolChangedEvent?.Invoke(poolNames[dropDownListSetting.dropdown.selectedIndex]);
        }

        [UIAction("clicked-rank-text")]
        private void RankTextClicked()
        {
            ClickedRankText?.Invoke(rankInfo, poolNames[dropDownListSetting.dropdown.selectedIndex]);
        }

        public void UserRegistered()
        {
            PromptText = "";
            LoadingActive = false;
        }

        public async void DifficultyBeatmapUpdated(IDifficultyBeatmap difficultyBeatmap, HitbloqLevelInfo levelInfoEntry)
        {
            poolInfoTokenSource?.Cancel();
            poolInfoTokenSource = new CancellationTokenSource();

            pools = new List<object>();
            rankInfo = null;

            if (levelInfoEntry != null)
            {
                foreach(var pool in levelInfoEntry.pools)
                {
                    HitbloqPoolInfo poolInfo = await poolInfoSource.GetPoolInfoAsync(pool.Key, poolInfoTokenSource.Token);
                    pools.Add($"{poolInfo.shownName} - {pool.Value}⭐");
                }
                poolNames = levelInfoEntry.pools.Keys.ToList();
                PoolUpdated(poolNames.First());
            }
            else
            {
                poolNames = new List<string> { "None" };
            }

            if (dropDownListSetting != null)
            {
                dropDownListSetting.values = pools.Count != 0 ? pools : new List<object> { "None" };
                dropDownListSetting.UpdateChoices();
                dropDownListSetting.dropdown.SelectCellWithIdx(0);

                if (!LoadingActive && !PromptText.Contains("<color=red>"))
                {
                    PromptText = "";
                }
            }
        }

        public async void PoolUpdated(string pool)
        {
            rankInfoTokenSource?.Cancel();
            rankInfoTokenSource = new CancellationTokenSource();
            rankInfo = await rankInfoSource.GetRankInfoForSelfAsync(pool, rankInfoTokenSource.Token);
            NotifyPropertyChanged(nameof(PoolRankingText));
        }

        public void LeaderboardEntriesUpdated(List<Entries.LeaderboardEntry> leaderboardEntries)
        {
            CuteMode = leaderboardEntries.Exists(u => u.userID == 726);
        }

        [UIValue("prompt-text")]
        public string PromptText
        {
            get => _promptText;
            set
            {
                _promptText = value;
                NotifyPropertyChanged(nameof(PromptText));
            }
        }

        [UIValue("loading-active")]
        public bool LoadingActive
        {
            get => _loadingActive;
            set
            {
                _loadingActive = value;
                NotifyPropertyChanged(nameof(LoadingActive));
            }
        }

        [UIValue("pool-ranking-text")]
        private string PoolRankingText => $"<b>Pool Ranking:</b> #{rankInfo?.rank} <size=75%>(<color=#aa6eff>{rankInfo?.cr.ToString("F2")}cr</color>)";

        [UIValue("pools")]
        private List<object> pools = new List<object> { "None" };
    }
}
