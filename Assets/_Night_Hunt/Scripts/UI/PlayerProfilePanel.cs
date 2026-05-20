using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.Utils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI
{
    public class PlayerProfilePanel : MonoBehaviour
    {
        public static PlayerProfilePanel Instance { get; private set; }

        [Header("Root (set active/inactive on show/hide)")]
        [SerializeField] private GameObject root;

        [Header("Backdrop — click-outside dismiss")]
        [SerializeField] private Button backdrop;

        [Header("Profile fields")]
        [SerializeField] private TMP_Text txt_Username;
        [SerializeField] private TMP_Text txt_ELO;
        [SerializeField] private TMP_Text txt_Tier;
        [SerializeField] private TMP_Text txt_WinLoss;
        [SerializeField] private TMP_Text txt_WinRate;
        [SerializeField] private Image    img_Character;

        [Header("Close button")]
        [SerializeField] private Button btn_Close;

        [Header("Loading indicator (optional)")]
        [SerializeField] private GameObject loadingIndicator;

        private IBackendClient _backendClient;
        private long           _currentUserId;
        private bool           _loading;
        private const string RuntimeRootName = "Runtime Player Profile Panel";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureRuntimeWiring();
            WireButtons();
        }

        private void Start()
        {
            EnsureRuntimeWiring();
            if (_backendClient == null && GameManager.Instance != null)
                _backendClient = GameManager.Instance.BackendClient;
            SetRootActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(long userId, string fallbackUsername = null)
        {
            EnsureRuntimeWiring();
            _currentUserId = userId;
            SetRootActive(true);
            ApplyPolishedRuntimeStyle();
            if (root != null) root.transform.SetAsLastSibling();
            else transform.SetAsLastSibling();
            SetPlaceholder(fallbackUsername ?? $"Player {userId}");
            _ = LoadProfileAsync(userId);
        }

        public void Hide()
        {
            SetRootActive(false);
            _currentUserId = 0;
        }

        private async Task LoadProfileAsync(long userId)
        {
            if (_loading) return;
            _loading = true;
            SetLoading(true);
            try
            {
                if (_backendClient == null) _backendClient = GameManager.Instance?.BackendClient;
                if (_backendClient == null) return;
                string endpoint = string.Format(Constants.API_PROFILE_PUBLIC, userId);
                var result = await _backendClient.GetAsync<ProfileResponse>(endpoint);
                if (_currentUserId != userId) return;
                if (result.Success && result.Data != null) PopulateProfile(result.Data);
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally { _loading = false; SetLoading(false); }
        }

        private void SetPlaceholder(string username)
        {
            if (txt_Username != null) txt_Username.text = username;
            if (txt_ELO != null) txt_ELO.text = "ELO: --";
            if (txt_Tier != null) txt_Tier.text = "Tier: Unranked";
            if (txt_WinLoss != null) txt_WinLoss.text = "Record: --W / --L";
            if (txt_WinRate != null) txt_WinRate.text = "Win Rate: --%";
            if (img_Character != null) img_Character.sprite = null;
        }

        private void PopulateProfile(ProfileResponse profile)
        {
            if (txt_Username != null) txt_Username.text = profile.username;
            if (txt_ELO != null) txt_ELO.text = $"ELO: {profile.elo}";
            if (txt_Tier != null) txt_Tier.text = $"Tier: {profile.tier ?? "Unranked"}";
            int w = profile.totalWins;
            int l = profile.totalLosses;
            if (txt_WinLoss != null) txt_WinLoss.text = $"Record: {w}W / {l}L";
            if (txt_WinRate != null)
            {
                float total = w + l;
                float rate = total > 0 ? (float)w / total * 100f : 0f;
                txt_WinRate.text = $"Win Rate: {rate:F1}%";
            }
            if (img_Character != null && !string.IsNullOrEmpty(profile.selectedCharacterId))
            {
                var def = NightHunt.Gameplay.Character.Data.CharacterDatabase.Instance?.GetById(profile.selectedCharacterId);
                if (def != null) img_Character.sprite = def.Thumbnail;
            }
        }

        private void SetRootActive(bool on) { if (root != null) root.SetActive(on); else gameObject.SetActive(on); }

        public void EnsureRuntimeWiring()
        {
            if (!HasRequiredReferences())
                BuildRuntimeHierarchy();
            ApplyPolishedRuntimeStyle();
            WireButtons();
        }

        private bool HasRequiredReferences()
        {
            return root != null && backdrop != null && txt_Username != null && txt_ELO != null && txt_Tier != null && txt_WinLoss != null && txt_WinRate != null && btn_Close != null && img_Character != null;
        }

        private void WireButtons()
        {
            if (btn_Close != null) { btn_Close.onClick.RemoveAllListeners(); btn_Close.onClick.AddListener(Hide); }
            if (backdrop != null) { backdrop.onClick.RemoveAllListeners(); backdrop.onClick.AddListener(Hide); }
        }

        private void BuildRuntimeHierarchy()
        {
            var canvas = GetComponentInParent<Canvas>(true) ?? UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            Transform parent = canvas != null ? canvas.transform : transform;
            var existing = parent.Find(RuntimeRootName);
            GameObject overlay = existing != null ? existing.gameObject : CreateUIObject(RuntimeRootName, parent);
            overlay.SetActive(false);
            Stretch(overlay.GetComponent<RectTransform>());
            root = overlay;
            var backdropGo = overlay.transform.Find("Backdrop") ?? CreateUIObject("Backdrop", overlay.transform).transform;
            Stretch(backdropGo.GetComponent<RectTransform>());
            var backdropImage = backdropGo.GetComponent<Image>() ?? backdropGo.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.7f);
            backdropImage.raycastTarget = true;
            backdrop = backdropGo.GetComponent<Button>() ?? backdropGo.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            var panelGo = overlay.transform.Find("Panel") ?? CreateUIObject("Panel", overlay.transform).transform;
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520, 420);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>() ?? panelGo.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.15f, 1f);
            EnsureHeader(panelGo);
            EnsureContent(panelGo);
            panelGo.SetAsLastSibling();
        }

        private void EnsureHeader(Transform panel)
        {
            var header = panel.Find("Header") ?? CreateUIObject("Header", panel).transform;
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0, 1); rt.sizeDelta = new Vector2(0, 50); rt.anchoredPosition = Vector2.zero;
            if (header.GetComponent<Image>() == null) header.gameObject.AddComponent<Image>();
            var close = header.Find("Close") ?? CreateUIObject("Close", header).transform;
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f); crt.sizeDelta = new Vector2(40, 40); crt.anchoredPosition = new Vector2(-10, 0);
            btn_Close = close.GetComponent<Button>() ?? close.gameObject.AddComponent<Button>();
        }

        private void EnsureContent(Transform panel)
        {
            var content = panel.Find("Content") ?? CreateUIObject("Content", panel).transform;
            var rt = content.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(20, 20); rt.offsetMax = new Vector2(-20, -60);
            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.spacing = 10;
            var charGo = content.Find("Char") ?? CreateUIObject("Char", content).transform;
            if (charGo.GetComponent<LayoutElement>() == null) charGo.gameObject.AddComponent<LayoutElement>();
            charGo.GetComponent<LayoutElement>().preferredHeight = 120;
            img_Character = charGo.GetComponent<Image>() ?? charGo.gameObject.AddComponent<Image>();
            img_Character.preserveAspect = true;
            txt_Username = CreateLabel(content, "User", "Player", 24);
            txt_ELO = CreateLabel(content, "ELO", "ELO: --", 18);
            txt_Tier = CreateLabel(content, "Tier", "Unranked", 18);
            txt_WinLoss = CreateLabel(content, "WL", "--W / --L", 18);
            txt_WinRate = CreateLabel(content, "WR", "--%", 18);
        }

        private TMP_Text CreateLabel(Transform parent, string name, string text, float size)
        {
            var go = parent.Find(name) ?? CreateUIObject(name, parent).transform;
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private void ApplyPolishedRuntimeStyle()
        {
            var panel = ResolvePanelTransform();
            if (panel != null)
            {
                var rt = panel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(520f, 420f);
                    rt.anchoredPosition = Vector2.zero;
                }

                var image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
                image.color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
            }

            StyleText(txt_Username, 30f, new Color(0.85f, 0.96f, 1f, 1f), FontStyles.Bold);
            StyleText(txt_ELO, 19f, new Color(0.35f, 0.82f, 1f, 1f), FontStyles.Bold);
            StyleText(txt_Tier, 18f, Color.white, FontStyles.Normal);
            StyleText(txt_WinLoss, 18f, Color.white, FontStyles.Normal);
            StyleText(txt_WinRate, 18f, Color.white, FontStyles.Normal);

            if (img_Character != null)
            {
                img_Character.color = new Color(1f, 1f, 1f, img_Character.sprite != null ? 1f : 0.18f);
                img_Character.preserveAspect = true;
                var rt = img_Character.GetComponent<RectTransform>();
                if (rt != null)
                    rt.sizeDelta = new Vector2(140f, 140f);
            }

            if (btn_Close != null)
            {
                var closeImage = btn_Close.GetComponent<Image>() ?? btn_Close.gameObject.AddComponent<Image>();
                closeImage.color = new Color(0.42f, 0.1f, 0.13f, 0.95f);
                var closeText = btn_Close.GetComponentInChildren<TMP_Text>(true);
                if (closeText == null)
                    closeText = CreateLabel(btn_Close.transform, "Close Label", "X", 18f);
                closeText.text = "X";
                closeText.alignment = TextAlignmentOptions.Center;
                closeText.color = Color.white;
                Stretch(closeText.GetComponent<RectTransform>());
            }
        }

        private Transform ResolvePanelTransform()
        {
            if (root != null)
            {
                var named = root.transform.Find("Panel");
                if (named != null)
                    return named;
            }

            Transform current = txt_Username != null ? txt_Username.transform : transform;
            while (current != null)
            {
                if (current.GetComponent<Image>() != null)
                    return current;
                if (root != null && current == root.transform)
                    break;
                current = current.parent;
            }

            return root != null ? root.transform : transform;
        }

        private static void StyleText(TMP_Text text, float size, Color color, FontStyles style)
        {
            if (text == null) return;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private GameObject CreateUIObject(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return go; }
        private void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private void SetLoading(bool on) { if (loadingIndicator != null) loadingIndicator.SetActive(on); }
    }
}
