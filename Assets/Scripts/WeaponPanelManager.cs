using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WeaponPanelManager : MonoBehaviour
{
    const string DefaultWeaponId = "starter_gun";

    GameObject weaponPanel;
    TMP_Text weaponNameText;
    TMP_Text descText;
    TMP_Text damageText;
    TMP_Text fireSpeedText;
    TMP_Text rangeText;
    TMP_Text pierceText;
    TMP_Text projectileCountText;
    Button purchaseButton;
    Button levelUpButton;
    Button equipButton;
    Button nextButton;
    Button prevButton;
    Transform weaponVisualRoot;
    GameObject unlockContent;
    TMP_Text unlockTitleText;
    Image unlockIcon1Image;
    Image unlockIcon2Image;
    TMP_Text unlockValue1Text;
    TMP_Text unlockValue2Text;

    readonly List<GameData.WeaponDef> weapons = new List<GameData.WeaponDef>();
    int currentIndex;
    bool currentWeaponAffordable;

    void OnEnable()
    {
        if (weaponPanel == null)
            weaponPanel = gameObject;
        AutoBind();
        ReloadWeapons();
        Refresh();
    }

    public void Init(GameObject panel)
    {
        weaponPanel = panel;
        AutoBind();
        ReloadWeapons();
        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, weapons.Count - 1));
        Refresh();
    }

    public void AutoBind()
    {
        if (weaponPanel == null)
            weaponPanel = GameObject.Find("weaponPanel");
        if (weaponPanel == null) return;

        weaponNameText = FindTmp("weaponName");
        descText = FindTmp("descContent", "desc", "description");
        damageText = FindTmp("atk", "damage");
        fireSpeedText = FindTmp("atkSpeed", "fireInterval", "fireSpeed");
        rangeText = FindTmp("ability1", "range", "detectRange");
        pierceText = FindTmp("ability2", "pierce", "pierceCount");
        projectileCountText = FindTmp("unlock1", "projCount", "projectileCount");
        weaponVisualRoot = FindChildTransform("weapon", "weaponImage", "weaponPreview", "weaponRoot");
        BindUnlockContent();

        purchaseButton = FindButton("purchaseBtn", "buyBtn", "btnPurchase");
        levelUpButton = FindButton("levelUpBtn", "upgradeBtn", "btnLevelUp");
        equipButton = FindButton("equipBtn", "equipButton", "btnEquip");
        nextButton = FindButton("nextWeapon", "rightWeapon", "nextBtn");
        prevButton = FindButton("lastWeapon", "prevWeapon", "leftWeapon", "prevBtn");

        BindButton(purchaseButton, PurchaseCurrent);
        BindButton(levelUpButton, () => { /* 강화 데이터는 추후 연결 */ });
        BindButton(equipButton, EquipCurrent);
        BindButton(nextButton, NextWeapon);
        BindButton(prevButton, PrevWeapon);
    }

    void ReloadWeapons()
    {
        weapons.Clear();
        var list = GameData.GetWeapons();
        if (list != null)
            weapons.AddRange(list);
    }

    public void Refresh()
    {
        if (weaponPanel == null) return;
        if (weapons.Count == 0)
        {
            SetText(weaponNameText, "무기 없음");
            SetText(descText, "weapon.csv를 확인해 주세요.");
            SetButtonVisible(purchaseButton, false);
            SetButtonVisible(levelUpButton, false);
            SetButtonVisible(equipButton, false);
            SetButtonVisible(nextButton, false);
            SetButtonVisible(prevButton, false);
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, weapons.Count - 1);
        var weapon = weapons[currentIndex];
        bool owned = IsOwned(weapon.weaponId);
        bool equipped = GetEquippedWeaponId() == weapon.weaponId;
        currentWeaponAffordable = IsWeaponAffordable(weapon);

        SetText(weaponNameText, string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponId : weapon.weaponName);
        SetText(descText, weapon.desc);
        SetText(damageText, $"공격력 : {weapon.damage}");
        SetText(fireSpeedText, $"공격속도 : {weapon.fireInterval:0.##}");
        SetText(rangeText, $"사거리 : {weapon.detectRange:0.##}");
        SetText(pierceText, $"관통 : {weapon.pierceCount}");
        SetText(projectileCountText, $"투사체 : {weapon.projCount}");
        RefreshWeaponVisual(weapon);
        RefreshUnlockContent(weapon, owned);

        SetButtonVisible(purchaseButton, !owned);
        SetButtonVisible(levelUpButton, owned);
        SetButtonVisible(equipButton, owned);
        SetButtonVisible(prevButton, currentIndex > 0);
        SetButtonVisible(nextButton, currentIndex < weapons.Count - 1);

        SetButtonText(purchaseButton, currentWeaponAffordable ? "구매하기" : "광석 부족");
        SetButtonText(levelUpButton, "강화");
        SetButtonText(equipButton, equipped ? "장착중" : "장착");
        if (purchaseButton != null)
            purchaseButton.interactable = !owned && currentWeaponAffordable;
        if (equipButton != null)
            equipButton.interactable = owned && !equipped;
    }

    void BindUnlockContent()
    {
        var root = FindChildTransform("unlockContent", "unlockPanel", "needContent", "costContent");
        unlockContent = root != null ? root.gameObject : null;
        if (root == null) return;

        unlockTitleText = FindTmpIn(root, "title", "Title", "unlockTitle", "needTitle");
        unlockIcon1Image = FindImageIn(root, "icon1", "unlockIcon1", "needIcon1", "oreIcon1", "resourceIcon1");
        unlockIcon2Image = FindImageIn(root, "icon2", "unlockIcon2", "needIcon2", "oreIcon2", "resourceIcon2");
        unlockValue1Text = FindTmpIn(root, "value1", "unlockValue1", "needValue1", "amount1", "cost1", "resourceAmount1");
        unlockValue2Text = FindTmpIn(root, "value2", "unlockValue2", "needValue2", "amount2", "cost2", "resourceAmount2");

        // 이름이 조금 달라도 unlockContent 안의 구성 순서로 최대한 자동 연결한다.
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        if (unlockTitleText == null && texts.Length > 0)
            unlockTitleText = texts[0];
        if (unlockValue1Text == null && texts.Length > 1)
            unlockValue1Text = texts[1];
        if (unlockValue2Text == null && texts.Length > 2)
            unlockValue2Text = texts[2];

        var images = root.GetComponentsInChildren<Image>(true);
        var costImages = new List<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null || images[i].transform == root) continue;
            costImages.Add(images[i]);
        }
        if (unlockIcon1Image == null && costImages.Count > 0)
            unlockIcon1Image = costImages[0];
        if (unlockIcon2Image == null && costImages.Count > 1)
            unlockIcon2Image = costImages[1];
    }

    void RefreshUnlockContent(GameData.WeaponDef weapon, bool owned)
    {
        if (unlockContent == null || weapon == null) return;

        bool show = !owned;
        unlockContent.SetActive(show);
        if (!show) return;

        var costs = GameData.GetWeaponUnlockCosts(weapon.weaponId);

        SetText(unlockTitleText, "필요 자원");
        RefreshUnlockSlot(unlockIcon1Image, unlockValue1Text, costs.Count > 0 ? costs[0] : null);
        RefreshUnlockSlot(unlockIcon2Image, unlockValue2Text, costs.Count > 1 ? costs[1] : null);
    }

    void RefreshUnlockSlot(Image iconImage, TMP_Text valueText, GameData.WeaponUnlockCost cost)
    {
        bool hasData = cost != null && !string.IsNullOrWhiteSpace(cost.materialId) && cost.amount > 0;
        int currentAmount = hasData && GameFlowManager.Instance != null
            ? GameFlowManager.Instance.GetMaterialAmount(cost.materialId)
            : 0;

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(hasData);
            if (hasData)
            {
                var sprite = LoadIconSprite(MaterialToIconId(cost.materialId));
                if (sprite != null)
                    iconImage.sprite = sprite;
                iconImage.preserveAspect = true;
            }
        }

        if (valueText != null)
        {
            valueText.gameObject.SetActive(hasData);
            valueText.text = hasData ? cost.amount.ToString() : "";
            valueText.color = !hasData || currentAmount >= cost.amount ? Color.white : new Color(1f, 0.35f, 0.35f, 1f);
        }
    }

    Sprite LoadIconSprite(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId)) return null;
        iconId = iconId.Trim();

        var sprite = Resources.Load<Sprite>($"icon/{iconId}");
        if (sprite != null) return sprite;

        var sprites = Resources.LoadAll<Sprite>($"icon/{iconId}");
        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == iconId)
                    return sprites[i];
                if (sprites[i] != null && sprites[i].name == $"{iconId}_0")
                    return sprites[i];
            }
            return sprites[0];
        }

        return null;
    }

    void PurchaseCurrent()
    {
        var weapon = CurrentWeapon();
        if (weapon == null) return;
        if (IsOwned(weapon.weaponId)) return;

        if (!TrySpendWeaponUnlockCosts(weapon))
        {
            Refresh();
            return;
        }

        SetOwned(weapon.weaponId, true);
        if (string.IsNullOrEmpty(GetEquippedWeaponId()))
            SetEquippedWeaponId(weapon.weaponId);
        SavePrefs();
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.SaveCurrentSlot();
        Refresh();
    }

    bool IsWeaponAffordable(GameData.WeaponDef weapon)
    {
        if (weapon == null) return false;
        if (IsOwned(weapon.weaponId)) return true;

        var costs = GameData.GetWeaponUnlockCosts(weapon.weaponId);
        if (costs.Count == 0) return true;
        if (GameFlowManager.Instance == null) return false;

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost == null) continue;
            if (!GameFlowManager.Instance.HasMaterial(cost.materialId, cost.amount))
                return false;
        }
        return true;
    }

    bool TrySpendWeaponUnlockCosts(GameData.WeaponDef weapon)
    {
        if (!IsWeaponAffordable(weapon)) return false;
        var costs = GameData.GetWeaponUnlockCosts(weapon.weaponId);
        if (costs.Count == 0) return true;
        if (GameFlowManager.Instance == null) return false;

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost == null) continue;
            if (!GameFlowManager.Instance.SpendMaterial(cost.materialId, cost.amount))
                return false;
        }
        return true;
    }

    void EquipCurrent()
    {
        var weapon = CurrentWeapon();
        if (weapon == null || !IsOwned(weapon.weaponId)) return;
        SetEquippedWeaponId(weapon.weaponId);
        SavePrefs();
        Refresh();
    }

    void NextWeapon()
    {
        if (currentIndex >= weapons.Count - 1) return;
        currentIndex++;
        Refresh();
    }

    void PrevWeapon()
    {
        if (currentIndex <= 0) return;
        currentIndex--;
        Refresh();
    }

    GameData.WeaponDef CurrentWeapon()
    {
        if (weapons.Count == 0) return null;
        currentIndex = Mathf.Clamp(currentIndex, 0, weapons.Count - 1);
        return weapons[currentIndex];
    }

    TMP_Text FindTmp(params string[] names)
    {
        var go = FindChild(names);
        if (go == null) return null;
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null) return tmp;
        return go.GetComponentInChildren<TMP_Text>(true);
    }

    TMP_Text FindTmpIn(Transform root, params string[] names)
    {
        var t = FindChildTransformIn(root, names);
        if (t == null) return null;
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) return tmp;
        return t.GetComponentInChildren<TMP_Text>(true);
    }

    Image FindImageIn(Transform root, params string[] names)
    {
        var t = FindChildTransformIn(root, names);
        if (t == null) return null;
        var image = t.GetComponent<Image>();
        if (image != null) return image;
        return t.GetComponentInChildren<Image>(true);
    }

    Transform FindChildTransformIn(Transform root, params string[] names)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            for (int n = 0; n < names.Length; n++)
            {
                if (all[i].name == names[n])
                    return all[i];
            }
        }
        return null;
    }

    Button FindButton(params string[] names)
    {
        var go = FindChild(names);
        if (go == null) return null;
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        return btn;
    }

    GameObject FindChild(params string[] names)
    {
        var t = FindChildTransform(names);
        return t != null ? t.gameObject : null;
    }

    Transform FindChildTransform(params string[] names)
    {
        if (weaponPanel == null) return null;
        var all = weaponPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            for (int n = 0; n < names.Length; n++)
            {
                if (all[i].name == names[n])
                    return all[i];
            }
        }
        return null;
    }

    void RefreshWeaponVisual(GameData.WeaponDef weapon)
    {
        if (weaponVisualRoot == null || weapon == null) return;

        bool owned = IsOwned(weapon.weaponId);
        bool foundMatch = false;
        for (int i = 0; i < weaponVisualRoot.childCount; i++)
        {
            var child = weaponVisualRoot.GetChild(i);
            bool isCurrent = IsWeaponVisualMatch(child.name, weapon);
            child.gameObject.SetActive(isCurrent);
            if (isCurrent)
            {
                ApplyWeaponVisualLockColor(child, owned);
                foundMatch = true;
            }
        }

        // If there are no weapon-id children yet, keep the manually placed preview as-is.
        if (!foundMatch && weaponVisualRoot.childCount == 0)
        {
            weaponVisualRoot.gameObject.SetActive(true);
            ApplyWeaponVisualLockColor(weaponVisualRoot, owned);
        }
    }

    void ApplyWeaponVisualLockColor(Transform root, bool owned)
    {
        if (root == null) return;

        Color color = owned ? Color.white : Color.black;
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].color = color;
        }

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = color;
        }
    }

    bool IsWeaponVisualMatch(string objectName, GameData.WeaponDef weapon)
    {
        if (weapon == null || string.IsNullOrEmpty(objectName)) return false;
        string normalizedObjectName = NormalizeVisualName(objectName);
        string normalizedWeaponId = NormalizeVisualName(weapon.weaponId);
        string normalizedIconKey = NormalizeVisualName(weapon.iconKey);

        return normalizedObjectName == normalizedWeaponId ||
               (!string.IsNullOrEmpty(normalizedIconKey) && normalizedObjectName == normalizedIconKey);
    }

    string NormalizeVisualName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        int cloneIndex = value.IndexOf("(", System.StringComparison.Ordinal);
        if (cloneIndex >= 0)
            value = value.Substring(0, cloneIndex);
        return value.Trim().ToLowerInvariant();
    }

    string MaterialToIconId(string materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId)) return "";
        switch (materialId.Trim().ToLowerInvariant())
        {
            case "stone": return "stone_1";
            case "copper": return "copper_node";
            case "gold": return "gold_node";
            default: return materialId.Trim();
        }
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null) return;
        button.interactable = true;
        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
            if (button.targetGraphic == null)
                button.targetGraphic = img;
        }
        var childGraphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < childGraphics.Length; i++)
        {
            var g = childGraphics[i];
            if (g != null && g.gameObject != button.gameObject)
                g.raycastTarget = false;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    void SetButtonVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    void SetButtonText(Button button, string value)
    {
        if (button == null) return;
        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = value;
        var legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = value;
    }

    static int Slot => GameFlowManager.CurrentSlot >= 1 ? GameFlowManager.CurrentSlot : 1;

    static string OwnedKey(int slot, string weaponId) => $"slot_{slot}_weapon_owned_{weaponId}";
    static string EquippedKey(int slot) => $"slot_{slot}_weapon_equipped";

    public static bool IsOwned(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return false;
        if (weaponId == DefaultWeaponId) return true;
        if (Slot < 1) return false;
        return PlayerPrefs.GetInt(OwnedKey(Slot, weaponId), 0) == 1;
    }

    static void SetOwned(string weaponId, bool owned)
    {
        if (string.IsNullOrEmpty(weaponId) || Slot < 1) return;
        PlayerPrefs.SetInt(OwnedKey(Slot, weaponId), owned ? 1 : 0);
    }

    public static string GetEquippedWeaponId()
    {
        if (Slot < 1) return DefaultWeaponId;
        string equipped = PlayerPrefs.GetString(EquippedKey(Slot), DefaultWeaponId);
        if (string.IsNullOrEmpty(equipped)) equipped = DefaultWeaponId;
        if (!IsOwned(equipped)) equipped = DefaultWeaponId;
        return equipped;
    }

    static void SetEquippedWeaponId(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId) || Slot < 1) return;
        PlayerPrefs.SetString(EquippedKey(Slot), weaponId);
    }

    static void SavePrefs()
    {
        PlayerPrefs.Save();
    }

    public static void DeleteSlotWeaponData(int slot)
    {
        if (slot < 1) return;
        PlayerPrefs.DeleteKey(EquippedKey(slot));
        var list = GameData.GetWeapons();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || string.IsNullOrEmpty(list[i].weaponId)) continue;
            PlayerPrefs.DeleteKey(OwnedKey(slot, list[i].weaponId));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallOnUpgradeScene()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name != "UpgradeScene") return;
            var panel = GameObject.Find("weaponPanel");
            if (panel == null) return;
            var manager = panel.GetComponent<WeaponPanelManager>();
            if (manager == null) manager = panel.AddComponent<WeaponPanelManager>();
            manager.Init(panel);
        };
    }
}
