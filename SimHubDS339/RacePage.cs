using System;
using System.Collections.Generic;
using System.Linq;

namespace SimHubDS339
{
    /// <summary>
    /// レース画面のページ種別。
    /// </summary>
    internal enum RacePage
    {
        /// <summary>メイン (既存のレース画面)</summary>
        Main,
        /// <summary>タイヤとブレーキ</summary>
        Tyres,
        /// <summary>燃料・スティント</summary>
        Fuel,
        /// <summary>デルタ・ラップ・ギャップ</summary>
        Delta,
        /// <summary>セッション・環境・車両設定</summary>
        Session,
    }

    /// <summary>
    /// ページ切替時に画面上部へ一時的に表示するオーバーレイ情報。
    /// </summary>
    internal sealed class PageOverlay
    {
        /// <summary>表示するページ名</summary>
        public string Title { get; }

        /// <summary>オーバーレイの有効期限 (UTC)</summary>
        public DateTime ExpireAt { get; }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="title">表示するページタイトル</param>
        /// <param name="duration">表示継続時間</param>
        public PageOverlay(string title, TimeSpan duration)
        {
            Title = title;
            ExpireAt = DateTime.UtcNow + duration;
        }

        /// <summary>現在有効 (表示中) かどうか</summary>
        public bool IsActive => DateTime.UtcNow < ExpireAt;
    }

    /// <summary>
    /// レース画面の現在ページおよび有効ページの状態を管理するスレッドセーフなクラス。
    /// </summary>
    internal sealed class PageState
    {
        private readonly object _lock = new();
        private RacePage _currentPage = RacePage.Main;
        private readonly HashSet<RacePage> _enabledPages = new() { RacePage.Main };
        private PageOverlay? _overlay;
        private static readonly TimeSpan OverlayDuration = TimeSpan.FromSeconds(1.5);

        /// <summary>
        /// ページが変更されたときに発生するイベント (引数: 新しい現在ページ)。
        /// </summary>
        public event Action<RacePage>? PageChanged;

        /// <summary>
        /// コンストラクタ。初期有効ページと初期現在ページを設定する。
        /// </summary>
        /// <param name="initialPage">初期ページ</param>
        /// <param name="enabledPages">有効なページのコレクション (null の場合はすべて有効)</param>
        public PageState(RacePage initialPage = RacePage.Main, IEnumerable<RacePage>? enabledPages = null)
        {
            if (enabledPages != null)
            {
                foreach (var p in enabledPages)
                {
                    _enabledPages.Add(p);
                }
            }
            else
            {
                // 既定では全ページを有効化
                foreach (RacePage p in Enum.GetValues(typeof(RacePage)))
                {
                    _enabledPages.Add(p);
                }
            }

            // Main は無効化不可
            _enabledPages.Add(RacePage.Main);

            if (_enabledPages.Contains(initialPage))
            {
                _currentPage = initialPage;
            }
            else
            {
                _currentPage = RacePage.Main;
            }
        }

        /// <summary>現在表示中のページ</summary>
        public RacePage CurrentPage
        {
            get
            {
                lock (_lock)
                {
                    return _currentPage;
                }
            }
        }

        /// <summary>現在有効なページのリスト (定義順)</summary>
        public IReadOnlyList<RacePage> EnabledPages
        {
            get
            {
                lock (_lock)
                {
                    return Enum.GetValues(typeof(RacePage))
                        .Cast<RacePage>()
                        .Where(p => _enabledPages.Contains(p))
                        .ToList();
                }
            }
        }

        /// <summary>
        /// 現在アクティブな切替オーバーレイを取得する (期限切れの場合は null)。
        /// </summary>
        public PageOverlay? GetActiveOverlay()
        {
            lock (_lock)
            {
                if (_overlay != null && _overlay.IsActive)
                {
                    return _overlay;
                }
                _overlay = null;
                return null;
            }
        }

        /// <summary>
        /// 次の有効なページへ切り替える。
        /// </summary>
        public void Next()
        {
            lock (_lock)
            {
                var list = GetOrderedEnabledPages();
                if (list.Count <= 1) return;

                int index = list.IndexOf(_currentPage);
                int nextIndex = (index + 1) % list.Count;
                SetPageInternal(list[nextIndex]);
            }
        }

        /// <summary>
        /// 前の有効なページへ切り替える。
        /// </summary>
        public void Previous()
        {
            lock (_lock)
            {
                var list = GetOrderedEnabledPages();
                if (list.Count <= 1) return;

                int index = list.IndexOf(_currentPage);
                int prevIndex = (index - 1 + list.Count) % list.Count;
                SetPageInternal(list[prevIndex]);
            }
        }

        /// <summary>
        /// 指定したページへ切り替える (有効でない場合は Main へフォールバック)。
        /// </summary>
        /// <param name="page">切り替え先ページ</param>
        public void SetPage(RacePage page)
        {
            lock (_lock)
            {
                if (!_enabledPages.Contains(page))
                {
                    page = RacePage.Main;
                }
                SetPageInternal(page);
            }
        }

        /// <summary>
        /// 有効なページの集合を更新する。現在ページが無効化された場合は Main に切り替える。
        /// </summary>
        /// <param name="pages">新しい有効ページ一覧</param>
        public void UpdateEnabledPages(IEnumerable<RacePage> pages)
        {
            lock (_lock)
            {
                _enabledPages.Clear();
                _enabledPages.Add(RacePage.Main); // Main は必須
                foreach (var p in pages)
                {
                    _enabledPages.Add(p);
                }

                // 現在ページが無効になったら Main へ戻す
                if (!_enabledPages.Contains(_currentPage))
                {
                    SetPageInternal(RacePage.Main);
                }
            }
        }

        /// <summary>
        /// ページの表示名を取得する。
        /// </summary>
        public static string GetPageTitle(RacePage page) => page switch
        {
            RacePage.Main => "MAIN",
            RacePage.Tyres => "TYRES",
            RacePage.Fuel => "FUEL",
            RacePage.Delta => "DELTA",
            RacePage.Session => "SESSION",
            _ => page.ToString().ToUpperInvariant(),
        };

        private void SetPageInternal(RacePage page)
        {
            bool changed = _currentPage != page;
            _currentPage = page;
            _overlay = new PageOverlay(GetPageTitle(page), OverlayDuration);

            if (changed)
            {
                PageChanged?.Invoke(page);
            }
        }

        private List<RacePage> GetOrderedEnabledPages()
        {
            return Enum.GetValues(typeof(RacePage))
                .Cast<RacePage>()
                .Where(p => _enabledPages.Contains(p))
                .ToList();
        }
    }
}
