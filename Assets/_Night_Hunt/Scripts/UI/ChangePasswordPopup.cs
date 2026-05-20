using System;
using System.Threading.Tasks;
using NightHunt.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class ChangePasswordPopup : MonoBehaviour
    {
        public static ChangePasswordPopup Instance { get; private set; }

        private const string RuntimeRootName = "Runtime Change Password Popup";

        [Header("Root")]
        [SerializeField] private GameObject root;

        [Header("Backdrop")]
        [SerializeField] private Button backdrop;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Body")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TMP_InputField oldPasswordInput;
        [SerializeField] private TMP_InputField newPasswordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Footer")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Func<string, string, string, Task<ApiResult>> _submitHandler;
        private bool _busy;

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
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void Show(Func<string, string, string, Task<ApiResult>> submitHandler)
        {
            if (submitHandler == null)
                return;

            var popup = Instance != null ? Instance : CreateRuntimeInstance();
            if (popup == null)
                return;

            popup.ShowInternal(submitHandler);
        }

        private static ChangePasswordPopup CreateRuntimeInstance()
        {
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                Debug.LogWarning("[ChangePasswordPopup] Cannot create runtime popup: no Canvas found.");
                return null;
            }

            var host = new GameObject(RuntimeRootName, typeof(RectTransform));
            host.layer = 5;
            host.transform.SetParent(canvas.transform, false);
            var popup = host.AddComponent<ChangePasswordPopup>();
            popup.BuildRuntimeHierarchy();
            popup.EnsureRuntimeWiring();
            return popup;
        }

        private void ShowInternal(Func<string, string, string, Task<ApiResult>> submitHandler)
        {
            _submitHandler = submitHandler;
            _busy = false;
            EnsureRuntimeWiring();
            ClearFields();
            SetStatus(string.Empty);
            SetRootActive(true);

            if (root != null)
                root.transform.SetAsLastSibling();
            else
                transform.SetAsLastSibling();
        }

        public void Hide()
        {
            HideImmediate();
            _submitHandler = null;
            _busy = false;
        }

        private void HideImmediate()
        {
            SetRootActive(false);
            ClearFields();
            SetStatus(string.Empty);
            SetBusy(false);
        }

        private void EnsureRuntimeWiring()
        {
            if (root == null || backdrop == null || titleText == null || messageText == null ||
                oldPasswordInput == null || newPasswordInput == null || confirmPasswordInput == null ||
                confirmButton == null || cancelButton == null || closeButton == null || statusText == null)
            {
                BuildRuntimeHierarchy();
            }

            WireButtons();
            SetBusy(false);
        }

        private void WireButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (backdrop != null)
            {
                backdrop.onClick.RemoveAllListeners();
                backdrop.onClick.AddListener(Hide);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(Hide);
            }
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
            backdropImage.color = new Color(0f, 0f, 0f, 0.72f);
            backdropImage.raycastTarget = true;
            backdrop = backdropGo.GetComponent<Button>() ?? backdropGo.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            var panelGo = overlay.transform.Find("Panel") ?? CreateUIObject("Panel", overlay.transform).transform;
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(560f, 420f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>() ?? panelGo.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.08f, 0.11f, 0.99f);

            EnsureHeader(panelGo);
            EnsureBody(panelGo);
            EnsureFooter(panelGo);
            panelGo.SetAsLastSibling();
        }

        private void EnsureHeader(Transform panel)
        {
            var header = panel.Find("Header") ?? CreateUIObject("Header", panel).transform;
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(0f, 52f);
            rt.anchoredPosition = Vector2.zero;

            if (header.GetComponent<Image>() == null)
                header.gameObject.AddComponent<Image>();

            titleText = header.Find("Title") != null
                ? header.Find("Title").GetComponent<TextMeshProUGUI>()
                : CreateLabel(header, "Title", "Change Password", 24f);
            titleText.text = "Change Password";
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            Stretch(titleText.GetComponent<RectTransform>());

            var close = header.Find("Close") ?? CreateUIObject("Close", header).transform;
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.sizeDelta = new Vector2(40f, 40f);
            crt.anchoredPosition = new Vector2(-10f, 0f);
            closeButton = close.GetComponent<Button>() ?? close.gameObject.AddComponent<Button>();

            var closeImage = close.GetComponent<Image>() ?? close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.42f, 0.1f, 0.13f, 0.95f);
            var closeLabel = close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel == null)
                closeLabel = CreateLabel(close, "Label", "X", 18f);
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.color = Color.white;
            Stretch(closeLabel.GetComponent<RectTransform>());
        }

        private void EnsureBody(Transform panel)
        {
            var body = panel.Find("Body") ?? CreateUIObject("Body", panel).transform;
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 84f);
            rt.offsetMax = new Vector2(-24f, -82f);

            var layout = body.GetComponent<VerticalLayoutGroup>() ?? body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;

            messageText = body.Find("Message") != null
                ? body.Find("Message").GetComponent<TextMeshProUGUI>()
                : CreateLabel(body, "Message", "Enter your current password and choose a new one.", 16f);
            messageText.color = new Color(0.8f, 0.9f, 0.95f, 1f);

            oldPasswordInput = CreatePasswordRow(body, "Old Password", "Current password");
            newPasswordInput = CreatePasswordRow(body, "New Password", "New password");
            confirmPasswordInput = CreatePasswordRow(body, "Confirm Password", "Confirm new password");

            statusText = body.Find("Status") != null
                ? body.Find("Status").GetComponent<TextMeshProUGUI>()
                : CreateLabel(body, "Status", string.Empty, 15f);
            statusText.color = new Color(0.95f, 0.65f, 0.45f, 1f);
        }

        private void EnsureFooter(Transform panel)
        {
            var footer = panel.Find("Footer") ?? CreateUIObject("Footer", panel).transform;
            var rt = footer.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 56f);
            rt.anchoredPosition = Vector2.zero;

            var layout = footer.GetComponent<HorizontalLayoutGroup>() ?? footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 10f;

            cancelButton = CreateButton(footer, "Cancel", Hide, new Color(0.22f, 0.28f, 0.34f, 0.95f));
            confirmButton = CreateButton(footer, "Change", OnConfirmClicked, new Color(0.14f, 0.34f, 0.44f, 0.98f));
        }

        private TMP_InputField CreatePasswordRow(Transform parent, string labelName, string placeholder)
        {
            var row = CreateUIObject(labelName.Replace(" ", string.Empty) + "Row", parent);
            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 4f;
            row.AddComponent<LayoutElement>().preferredHeight = 70f;

            var label = CreateLabel(row.transform, labelName + "Label", labelName, 15f);
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.75f, 0.88f, 0.95f, 1f);

            var fieldGo = CreateUIObject(labelName.Replace(" ", string.Empty) + "Field", row.transform);
            var fieldLayout = fieldGo.AddComponent<LayoutElement>();
            fieldLayout.preferredHeight = 38f;
            var fieldImage = fieldGo.AddComponent<Image>();
            fieldImage.color = new Color(0.11f, 0.18f, 0.24f, 0.98f);

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.Password;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.targetGraphic = fieldImage;

            var viewport = CreateUIObject("Text Area", fieldGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(12f, 6f);
            viewportRt.offsetMax = new Vector2(-12f, -6f);
            viewport.AddComponent<RectMask2D>();

            var textGo = CreateUIObject("Text", viewport.transform);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(textGo.GetComponent<RectTransform>());

            var placeholderGo = CreateUIObject("Placeholder", viewport.transform);
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 18f;
            placeholderText.color = new Color(0.55f, 0.65f, 0.72f, 0.75f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(placeholderGo.GetComponent<RectTransform>());

            input.textViewport = viewportRt;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.caretColor = Color.white;
            input.selectionColor = new Color(0.35f, 0.82f, 1f, 0.25f);

            return input;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var go = CreateUIObject(label + "Button", parent);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 40f;
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateLabel(go.transform, label + "Label", label, 18f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            Stretch(text.GetComponent<RectTransform>());
            return button;
        }

        private void OnConfirmClicked()
        {
            if (_busy)
                return;

            string oldPassword = oldPasswordInput != null ? oldPasswordInput.text.Trim() : string.Empty;
            string newPassword = newPasswordInput != null ? newPasswordInput.text.Trim() : string.Empty;
            string confirmPassword = confirmPasswordInput != null ? confirmPasswordInput.text.Trim() : string.Empty;

            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                SetStatus("Fill in all password fields.");
                return;
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                SetStatus("New password and confirmation do not match.");
                return;
            }

            if (_submitHandler == null)
            {
                SetStatus("Change password handler is missing.");
                return;
            }

            _ = SubmitAsync(oldPassword, newPassword, confirmPassword);
        }

        private async Task SubmitAsync(string oldPassword, string newPassword, string confirmPassword)
        {
            _busy = true;
            SetBusy(true);
            SetStatus("Changing password...");

            ApiResult result;
            try
            {
                result = await _submitHandler(oldPassword, newPassword, confirmPassword);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                result = ApiResult.Error(ex.Message);
            }

            _busy = false;
            SetBusy(false);

            if (result != null && result.Success)
            {
                HideImmediate();
                var modal = GameModalWindow.Instance;
                if (modal != null)
                {
                    modal.ShowNotice(
                        "Password changed",
                        "Your session will be closed. Please sign in again.",
                        onClose: LoginView.Logout);
                }
                else
                {
                    LoginView.Logout();
                }
                return;
            }

            SetStatus(result?.Message ?? "Unable to change password.");
        }

        private void SetBusy(bool busy)
        {
            if (confirmButton != null)
                confirmButton.interactable = !busy;
            if (cancelButton != null)
                cancelButton.interactable = !busy;
            if (closeButton != null)
                closeButton.interactable = !busy;

            if (oldPasswordInput != null) oldPasswordInput.interactable = !busy;
            if (newPasswordInput != null) newPasswordInput.interactable = !busy;
            if (confirmPasswordInput != null) confirmPasswordInput.interactable = !busy;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message ?? string.Empty;
        }

        private void ClearFields()
        {
            if (oldPasswordInput != null) oldPasswordInput.text = string.Empty;
            if (newPasswordInput != null) newPasswordInput.text = string.Empty;
            if (confirmPasswordInput != null) confirmPasswordInput.text = string.Empty;
        }

        private void SetRootActive(bool on)
        {
            if (root != null)
                root.SetActive(on);
            else
                gameObject.SetActive(on);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size)
        {
            var go = parent.Find(name) ?? CreateUIObject(name, parent).transform;
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
            return tmp;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
