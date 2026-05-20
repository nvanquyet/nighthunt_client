using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

namespace Michsky.MUIP
{
    public class CustomDropdown : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        // Resources
        public Animator dropdownAnimator;
        public GameObject triggerObject;
        public TextMeshProUGUI selectedText;
        public Image selectedImage;
        public Transform itemParent;
        public GameObject itemObject;
        public GameObject scrollbar;
        public VerticalLayoutGroup itemList;
        public AudioSource soundSource;
        public RectTransform listRect;
        public CanvasGroup listCG;
        public CanvasGroup contentCG;

        // Settings
        public bool isInteractable = true;
        public bool enableIcon = true;
        public bool enableTrigger = true;
        public bool enableScrollbar = true;
        public bool updateOnEnable = true;
        public bool outOnPointerExit = false;
        public bool setHighPriority = true;
        public bool invokeAtStart = false;
        public bool initAtStart = true;
        public bool enableDropdownSounds = false;
        public bool useHoverSound = true;
        public bool useClickSound = true;
        [Range(1, 50)] public int itemPaddingTop = 8;
        [Range(1, 50)] public int itemPaddingBottom = 8;
        [Range(1, 50)] public int itemPaddingLeft = 8;
        [Range(1, 50)] public int itemPaddingRight = 25;
        [Range(1, 50)] public int itemSpacing = 8;
        public int selectedItemIndex = 0;

        // Animation
        public AnimationType animationType;
        public PanelDirection panelDirection;
        [Range(25, 1000)] public float panelSize = 200;
        [Range(0.5f, 10)] public float curveSpeed = 3;
        public AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 1.0f));

        // Saving
        public bool saveSelected = false;
        public string saveKey = "My Dropdown";

        // Item list
        [SerializeField]
        public List<Item> items = new List<Item>();

        // Events
        [System.Serializable] public class DropdownEvent : UnityEvent<int> { }
        public DropdownEvent onValueChanged = new DropdownEvent();
        [System.Serializable] public class ItemTextChangedEvent : UnityEvent<TMP_Text> { }
        public ItemTextChangedEvent onItemTextChanged = new ItemTextChangedEvent();

        // Audio
        public AudioClip hoverSound;
        public AudioClip clickSound;

        // Helpers
        bool isInitialized = false;
        [HideInInspector] public bool isOn;
        [HideInInspector] public int index = 0;
        [HideInInspector] public int siblingIndex = 0;
        [HideInInspector] public TextMeshProUGUI setItemText;
        [HideInInspector] public Image setItemImage;
        EventTrigger triggerEvent;
        Sprite imageHelper;
        string textHelper;
        GameObject runtimeItemTemplate;

#if UNITY_EDITOR
        public bool extendEvents = false;
#endif

        public enum AnimationType { Modular, Custom }
        public enum PanelDirection { Bottom, Top }

        [System.Serializable]
        public class Item
        {
            public string itemName = "Dropdown Item";
            public Sprite itemIcon;
            [HideInInspector] public int itemIndex;
            public UnityEvent OnItemSelection = new UnityEvent();
        }

        void OnEnable()
        {
            if (!isInitialized) { Initialize(); }
            if (updateOnEnable && index < items.Count) { SetDropdownIndex(selectedItemIndex, false); }

            SyncPanelSizeFromListRect();
            listCG.alpha = 0;
            listCG.interactable = false;
            listCG.blocksRaycasts = false;
            listCG.gameObject.SetActive(false);
            PreserveClosedListSize();
        }

        void Initialize()
        {
            if (enableTrigger && triggerObject != null)
            {
                // triggerButton = gameObject.GetComponent<Button>();
                triggerEvent = triggerObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerClick;
                entry.callback.AddListener((eventData) => { Animate(); });
                triggerEvent.GetComponent<EventTrigger>().triggers.Add(entry);
            }

            if (setHighPriority)
            {
                if (contentCG == null) { contentCG = transform.Find("Content/Item List").GetComponent<CanvasGroup>(); }
                contentCG.alpha = 1;

                Canvas tempCanvas = contentCG.GetComponent<Canvas>();
                if (tempCanvas == null) { tempCanvas = contentCG.gameObject.AddComponent<Canvas>(); }
                tempCanvas.overrideSorting = true;
                tempCanvas.sortingOrder = 30000;
                if (contentCG.GetComponent<GraphicRaycaster>() == null) { contentCG.gameObject.AddComponent<GraphicRaycaster>(); }
            }

            dropdownAnimator = gameObject.GetComponent<Animator>();

            if (listCG == null) { listCG = gameObject.GetComponentInChildren<CanvasGroup>(); }
            if (listRect == null) { listRect = listCG.GetComponent<RectTransform>(); }
            if (initAtStart && items.Count != 0) { SetupDropdown(); }
            if (animationType == AnimationType.Modular && dropdownAnimator != null) { Destroy(dropdownAnimator); }

            isInitialized = true;
        }

        public void SetupDropdown()
        {
            if (!enableScrollbar && scrollbar != null) { Destroy(scrollbar); }
            if (itemParent == null || itemObject == null) { return; }
            if (itemList == null) { itemList = itemParent.GetComponent<VerticalLayoutGroup>(); }

            UpdateItemLayout();
            index = 0;

            if (items == null || items.Count == 0)
            {
                if (selectedText != null) { selectedText.text = string.Empty; }
                return;
            }

            selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, items.Count - 1);
            GameObject templateObject = ResolveItemTemplate();
            if (templateObject == null) { return; }

            for (int i = itemParent.childCount - 1; i >= 0; i--)
            {
                Transform child = itemParent.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            for (int i = 0; i < items.Count; ++i)
            {
                GameObject go = Instantiate(templateObject, new Vector3(0, 0, 0), Quaternion.identity);
                go.transform.SetParent(itemParent, false);
                go.SetActive(true);
                go.name = items[i].itemName;

                setItemText = ResolvePrimaryText(go);
                textHelper = items[i].itemName;
                if (setItemText != null) { setItemText.text = textHelper; }

                if (setItemText != null) { onItemTextChanged?.Invoke(setItemText); }

                Transform goImage = go.gameObject.transform.Find("Icon");
                setItemImage = goImage != null ? goImage.GetComponent<Image>() : null;

                if (setItemImage != null)
                {
                    if (items[i].itemIcon == null) { setItemImage.gameObject.SetActive(false); }
                    else { imageHelper = items[i].itemIcon; setItemImage.sprite = imageHelper; }
                }
              
                items[i].itemIndex = i;
                Item mainItem = items[i];

                Button itemButton = go.GetComponent<Button>();
                if (itemButton != null)
                {
                    itemButton.onClick.AddListener(Animate);
                    itemButton.onClick.AddListener(items[i].OnItemSelection.Invoke);
                    itemButton.onClick.AddListener(delegate
                    {
                        SetDropdownIndex(index = mainItem.itemIndex);
                        onValueChanged.Invoke(index = mainItem.itemIndex);
                        if (saveSelected) { PlayerPrefs.SetInt("Dropdown_" + saveKey, mainItem.itemIndex); }
                    });
                }
            }

            if (selectedImage != null && !enableIcon) { selectedImage.gameObject.SetActive(false); }
            else if (selectedImage != null) { selectedImage.sprite = items[selectedItemIndex].itemIcon; }
            if (selectedText != null) { selectedText.text = items[selectedItemIndex].itemName; onItemTextChanged?.Invoke(selectedText); }
          
            if (saveSelected)
            {
                if (invokeAtStart) { items[PlayerPrefs.GetInt("Dropdown_" + saveKey)].OnItemSelection.Invoke(); }
                else { SetDropdownIndex(PlayerPrefs.GetInt("Dropdown_" + saveKey), false); }
            }
            else if (invokeAtStart) { items[selectedItemIndex].OnItemSelection.Invoke(); }
        }

        GameObject ResolveItemTemplate()
        {
            if (runtimeItemTemplate != null) { return runtimeItemTemplate; }
            if (itemObject == null) { return null; }

            if (itemParent != null && itemObject.transform.IsChildOf(itemParent))
            {
                if (runtimeItemTemplate == null)
                {
                    runtimeItemTemplate = Instantiate(itemObject, transform, false);
                    runtimeItemTemplate.name = "Runtime Dropdown Item Template";
                    runtimeItemTemplate.SetActive(false);
                }

                return runtimeItemTemplate;
            }

            if (itemObject.scene.IsValid()) { itemObject.SetActive(false); }
            return itemObject;
        }

        static TextMeshProUGUI ResolvePrimaryText(GameObject root)
        {
            if (root == null) { return null; }
            TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (labels == null || labels.Length == 0) { return null; }

            TextMeshProUGUI best = labels[0];
            float bestScore = float.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                TextMeshProUGUI label = labels[i];
                if (label == null) { continue; }

                float score = label.fontSize;
                RectTransform rect = label.rectTransform;
                if (rect != null)
                    score += Mathf.Abs(rect.rect.width) * 0.05f + Mathf.Abs(rect.rect.height) * 0.02f;

                string n = label.gameObject.name.ToLowerInvariant();
                if (n.Contains("title") || n.Contains("text") || n.Contains("label") || n.Contains("name"))
                    score += 100f;
                if (n.Contains("available") || n.Contains("status") || n.Contains("badge"))
                    score -= 200f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = label;
                }
            }

            return best;
        }

        // Obsolete
        public void ChangeDropdownInfo(int itemIndex)
        { 
            SetDropdownIndex(itemIndex); 
        }

        public void SetDropdownIndex(int itemIndex)
        {
            SetDropdownIndex(itemIndex, true);
        }

        public void SetDropdownIndex(int itemIndex, bool bypassSound = false)
        {
            if (items == null || items.Count == 0) { return; }
            itemIndex = Mathf.Clamp(itemIndex, 0, items.Count - 1);

            if (selectedImage != null && enableIcon && items[itemIndex].itemIcon != null) { selectedImage.gameObject.SetActive(true); selectedImage.sprite = items[itemIndex].itemIcon; }
            else if (selectedImage != null && enableIcon && items[itemIndex].itemIcon == null) { selectedImage.gameObject.SetActive(false); }
            if (selectedText != null) { selectedText.text = items[itemIndex].itemName; onItemTextChanged?.Invoke(selectedText); }
            if (!bypassSound && enableDropdownSounds && useClickSound) { soundSource.PlayOneShot(clickSound); }

            selectedItemIndex = itemIndex;
        }

        public void Animate()
        {
            if (!isOn && animationType == AnimationType.Modular)
            {
                isOn = true;
                listCG.blocksRaycasts = true;
                listCG.interactable = true;
                listCG.gameObject.SetActive(true);

                StopCoroutine("StartMinimize");
                StopCoroutine("StartExpand");
                StartCoroutine("StartExpand");
            }

            else if (isOn && animationType == AnimationType.Modular)
            {
                isOn = false;
                listCG.blocksRaycasts = false;
                listCG.interactable = false;

                StopCoroutine("StartMinimize");
                StopCoroutine("StartExpand");
                StartCoroutine("StartMinimize");
            }

            else if (!isOn && animationType == AnimationType.Custom)
            {   
                dropdownAnimator.Play("Stylish In");
                isOn = true;
            }

            else if (isOn && animationType == AnimationType.Custom)
            {
                dropdownAnimator.Play("Stylish Out");
                isOn = false;
            }

            if (enableTrigger && !isOn) { triggerObject.SetActive(false); }
            else if (enableTrigger && isOn) { triggerObject.SetActive(true); }
            if (enableTrigger && outOnPointerExit) { triggerObject.SetActive(false); }
        }

        public void Interactable(bool value)
        {
            isInteractable = value;
        }

        public void CreateNewItem(string title, Sprite icon, bool notify = false)
        {
            Item item = new Item
            {
                itemName = title,
                itemIcon = icon
            };
            items.Add(item);

            if (selectedItemIndex > items.Count) { selectedItemIndex = 0; }
            if (notify) { SetupDropdown(); }
        }

        public void CreateNewItem(string title, bool notify = false)
        {
            Item item = new Item
            {
                itemName = title
            };
            items.Add(item);

            if (selectedItemIndex > items.Count) { selectedItemIndex = 0; }
            if (notify) { SetupDropdown(); }
        }

        public void RemoveItem(string itemTitle, bool notify = false)
        {
            var item = items.Find(x => x.itemName == itemTitle);
            items.Remove(item);

            if (selectedItemIndex > items.Count) { selectedItemIndex = 0; }
            if (notify) { SetupDropdown(); }
        }

        public void UpdateItemLayout()
        {
            if (itemList == null)
                return;

            itemList.spacing = itemSpacing;
            itemList.padding.top = itemPaddingTop;
            itemList.padding.bottom = itemPaddingBottom;
            itemList.padding.left = itemPaddingLeft;
            itemList.padding.right = itemPaddingRight;
        }

        void SyncPanelSizeFromListRect()
        {
            if (listRect == null || isOn)
                return;

            float configuredHeight = listRect.sizeDelta.y;
            if (configuredHeight > 1f && Mathf.Abs(configuredHeight - panelSize) > 0.5f)
                panelSize = configuredHeight;
        }

        void PreserveClosedListSize()
        {
            if (listRect != null)
                listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, panelSize);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable) { return; }
            if (enableDropdownSounds && useClickSound) { soundSource.PlayOneShot(clickSound); }
            
            Animate();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) { return; }
            if (enableDropdownSounds && useHoverSound) { soundSource.PlayOneShot(hoverSound); }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable) { return; }
            if (outOnPointerExit && isOn) { Animate(); isOn = false; }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!isInteractable) { return; }
            if (enableDropdownSounds && useClickSound) { soundSource.PlayOneShot(clickSound); }

            Animate();
        }

        IEnumerator StartExpand()
        {
            float elapsedTime = 0;

            Vector2 startPos = new Vector2(listRect.sizeDelta.x, 0);
            Vector2 endPos = new Vector2(listRect.sizeDelta.x, panelSize);
            listRect.sizeDelta = startPos;

            while (listRect.sizeDelta.y <= panelSize - 0.1f)
            {
                elapsedTime += Time.unscaledDeltaTime;

                listCG.alpha += Time.unscaledDeltaTime * (curveSpeed * 2);
                listRect.sizeDelta = Vector2.Lerp(startPos, endPos, animationCurve.Evaluate(elapsedTime * curveSpeed));
                yield return null;
            }

            listCG.alpha = 1;
            listRect.sizeDelta = endPos;
        }

        IEnumerator StartMinimize()
        {
            float elapsedTime = 0;

            Vector2 startPos = listRect.sizeDelta;
            Vector2 endPos = new Vector2(listRect.sizeDelta.x, 0);

            while (listRect.sizeDelta.y >= 0.1f)
            {
                elapsedTime += Time.unscaledDeltaTime;

                listCG.alpha -= Time.unscaledDeltaTime * (curveSpeed * 2);
                listRect.sizeDelta = Vector2.Lerp(startPos, endPos, animationCurve.Evaluate(elapsedTime * curveSpeed));

                yield return null;
            }

            listCG.alpha = 0;
            listRect.sizeDelta = endPos;
            listCG.gameObject.SetActive(false);
            PreserveClosedListSize();
        }
    }
}
