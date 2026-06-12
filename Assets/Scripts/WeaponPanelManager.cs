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

    readonly List<GameData.WeaponDef> weapons = new List<GameData.WeaponDef>();
    int currentIndex;

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

        SetText(weaponNameText, string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponId : weapon.weaponName);
        SetText(descText, weapon.desc);
        SetText(damageText, $"공격력 : {weapon.damage}");
        SetText(fireSpeedText, $"공격속도 : {weapon.fireInterval:0.##}");
        SetText(rangeText, $"사거리 : {weapon.detectRange:0.##}");
        SetText(pierceText, $"관통 : {weapon.pierceCount}");
        SetText(projectileCountText, $"투사체 : {weapon.projCount}");

        SetButtonVisible(purchaseButton, !owned);
        SetButtonVisible(levelUpButton, owned);
        SetButtonVisible(equipButton, owned);
        SetButtonVisible(prevButton, currentIndex > 0);
        SetButtonVisible(nextButton, currentIndex < weapons.Count - 1);

        SetButtonText(purchaseButton, "구매하기");
        SetButtonText(levelUpButton, "강화");
        SetButtonText(equipButton, equipped ? "장착중" : "장착");
        if (equipButton != null)
            equipButton.interactable = owned && !equipped;
    }

    void PurchaseCurrent()
    {
        var weapon = CurrentWeapon();
        if (weapon == null) return;
        SetOwned(weapon.weaponId, true);
        if (string.IsNullOrEmpty(GetEquippedWeaponId()))
            SetEquippedWeaponId(weapon.weaponId);
        SavePrefs();
        Refresh();
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
        if (weaponPanel == null) return null;
        var all = weaponPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            for (int n = 0; n < names.Length; n++)
            {
                if (all[i].name == names[n])
                    return all[i].gameObject;
            }
        }
        return null;
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
