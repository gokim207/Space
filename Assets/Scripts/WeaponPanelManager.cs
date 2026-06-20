using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WeaponPanelManager : MonoBehaviour
{
    const string DefaultWeaponId = "starter_gun";
    public const int DefaultWeaponLevel = 1;

    GameObject weaponPanel;
    TMP_Text weaponNameText;
    TMP_Text descText;
    TMP_Text damageText;
    TMP_Text fireSpeedText;
    TMP_Text rangeText;
    TMP_Text pierceText;
    Button purchaseButton;
    Button levelUpButton;
    Button equipButton;
    Button nextButton;
    Button prevButton;
    Button traitOpenButton;
    Button traitCloseButton;
    Button traitRerollButton;
    Button traitLock1Button;
    Button traitLock2Button;
    Image traitLock1Image;
    Image traitLock2Image;
    Sprite traitLockedSprite;
    Sprite traitUnlockedSprite;
    Transform weaponVisualRoot;
    GameObject traitContent;
    TMP_Text traitDetail1Text;
    TMP_Text traitDetail2Text;
    TMP_Text traitTitle1Text;
    TMP_Text traitTitle2Text;
    TMP_Text traitDesc1Text;
    TMP_Text traitDesc2Text;
    GameObject traitLock1;
    GameObject traitLock2;
    GameObject unlockContent;
    TMP_Text unlockTitleText;
    Image unlockIcon1Image;
    Image unlockIcon2Image;
    TMP_Text unlockValue1Text;
    TMP_Text unlockValue2Text;
    GameObject levelUpContent;
    TMP_Text levelText;
    TMP_Text successRateText;
    TMP_Text failRateText;
    TMP_Text breakRateText;
    TMP_Text upgradeCostText;

    readonly List<GameData.WeaponDef> weapons = new List<GameData.WeaponDef>();
    int currentIndex;
    bool currentWeaponAffordable;
    bool currentWeaponCanUpgrade;

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
        weaponVisualRoot = FindChildTransform("weapon", "weaponImage", "weaponPreview", "weaponRoot");
        BindUnlockContent();
        BindLevelUpContent();
        BindTraitPopup();

        purchaseButton = FindButton("purchaseBtn", "buyBtn", "btnPurchase");
        levelUpButton = FindButton("levelUpBtn", "upgradeBtn", "btnLevelUp");
        equipButton = FindButton("equipBtn", "equipButton", "btnEquip");
        nextButton = FindButton("nextWeapon", "rightWeapon", "nextBtn");
        prevButton = FindButton("lastWeapon", "prevWeapon", "leftWeapon", "prevBtn");

        BindButton(purchaseButton, PurchaseCurrent);
        BindButton(levelUpButton, UpgradeCurrent);
        BindButton(equipButton, EquipCurrent);
        BindButton(nextButton, NextWeapon);
        BindButton(prevButton, PrevWeapon);
    }

    void BindTraitPopup()
    {
        Transform detailRoot = FindChildTransform("detailContent");
        Transform traitRoot = FindChildTransform("traitContent");
        traitContent = traitRoot != null ? traitRoot.gameObject : null;

        traitOpenButton = FindButtonIn(detailRoot, "Button");
        traitCloseButton = FindButtonIn(traitRoot, "Xbtn");
        traitRerollButton = FindButtonIn(traitRoot, "Button");
        traitLock1Button = FindButtonIn(traitRoot, "islock1", "isLock1");
        traitLock2Button = FindButtonIn(traitRoot, "islock2", "isLock2");
        traitLock1Image = FindImageIn(traitLock1Button != null ? traitLock1Button.transform : null, "lockImage", "lockimage");
        traitLock2Image = FindImageIn(traitLock2Button != null ? traitLock2Button.transform : null, "lockImage", "lockimage");
        traitLockedSprite = LoadIconSprite("lock");
        traitUnlockedSprite = LoadIconSprite("unlock");
        traitDetail1Text = FindTmpIn(detailRoot, "unlock1");
        traitDetail2Text = FindTmpIn(detailRoot, "unlock2");
        traitTitle1Text = FindTmpIn(traitRoot, "traitTitle1", "traitTitile1");
        traitTitle2Text = FindTmpIn(traitRoot, "traitTitle2", "traitTitile2");
        traitDesc1Text = FindTmpIn(traitRoot, "traitContent1");
        traitDesc2Text = FindTmpIn(traitRoot, "traitContent2");
        traitLock1 = FindChildTransformIn(traitRoot, "unlock1")?.gameObject;
        traitLock2 = FindChildTransformIn(traitRoot, "unlock2")?.gameObject;

        BindButton(traitOpenButton, ToggleTraitPopup);
        BindButton(traitCloseButton, CloseTraitPopup);
        BindButton(traitRerollButton, RerollTraits);
        BindButton(traitLock1Button, () => ToggleTraitLock(5));
        BindButton(traitLock2Button, () => ToggleTraitLock(10));

        if (traitContent != null && traitContent.activeSelf)
            traitContent.SetActive(false);
    }

    void ToggleTraitPopup()
    {
        if (traitContent == null || weapons.Count == 0)
            return;

        var weapon = weapons[Mathf.Clamp(currentIndex, 0, weapons.Count - 1)];
        if (!IsOwned(weapon.weaponId))
            return;

        traitContent.SetActive(!traitContent.activeSelf);
    }

    void CloseTraitPopup()
    {
        if (traitContent != null)
            traitContent.SetActive(false);
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
            SetButtonVisible(traitOpenButton, false);
            CloseTraitPopup();
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, weapons.Count - 1);
        var weapon = weapons[currentIndex];
        bool owned = IsOwned(weapon.weaponId);
        bool equipped = GetEquippedWeaponId() == weapon.weaponId;
        currentWeaponAffordable = IsWeaponAffordable(weapon);
        int currentLevel = ClampWeaponLevel(weapon.weaponId, GetWeaponLevel(weapon.weaponId));
        var upgrade = GameData.GetWeaponUpgrade(weapon.weaponId, currentLevel);
        bool hasNextUpgrade = CanUseUpgrade(upgrade, currentLevel);
        currentWeaponCanUpgrade = owned && hasNextUpgrade && GameFlowManager.Instance != null && GameFlowManager.Instance.GetMoney() + 0.0001f >= upgrade.upgradeCost;
        GetEffectiveStats(weapon, currentLevel, out int effectiveDamage, out float effectiveFireInterval);
        string damageDelta = owned && hasNextUpgrade ? FormatUpgradeDelta(upgrade.damageAdd, "0") : "";
        string fireSpeedDelta = owned && hasNextUpgrade ? FormatUpgradeDelta(-upgrade.fireIntervalAdd, "0.##") : "";

        SetText(weaponNameText, string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponId : weapon.weaponName);
        SetText(descText, weapon.desc);
        SetText(damageText, $"공격력 : {effectiveDamage}{damageDelta}");
        SetText(fireSpeedText, $"공격속도 : {effectiveFireInterval:0.##}s{fireSpeedDelta}");
        SetText(rangeText, $"사거리 : {weapon.detectRange:0.##}");
        SetText(pierceText, $"관통 : {weapon.pierceCount}");
        RefreshWeaponVisual(weapon);
        RefreshUnlockContent(weapon, owned);
        RefreshLevelUpContent(weapon, owned, currentLevel, upgrade);
        RefreshTraitAccess(owned);
        RefreshTraitDisplay(weapon, owned, currentLevel);

        SetButtonVisible(purchaseButton, !owned);
        SetButtonVisible(levelUpButton, owned);
        SetButtonVisible(equipButton, owned);
        SetButtonVisible(prevButton, currentIndex > 0);
        SetButtonVisible(nextButton, currentIndex < weapons.Count - 1);

        SetButtonText(purchaseButton, currentWeaponAffordable ? "구매하기" : "광석 부족");
        SetButtonText(levelUpButton, !hasNextUpgrade ? "최대 레벨" : currentWeaponCanUpgrade ? "강화" : "돈 부족");
        SetButtonText(equipButton, equipped ? "장착중" : "장착");
        if (purchaseButton != null)
            purchaseButton.interactable = !owned && currentWeaponAffordable;
        if (levelUpButton != null)
            levelUpButton.interactable = currentWeaponCanUpgrade;
        if (equipButton != null)
            equipButton.interactable = owned && !equipped;
    }

    void RefreshTraitAccess(bool owned)
    {
        SetButtonVisible(traitOpenButton, owned);
        if (!owned)
            CloseTraitPopup();
    }

    void RefreshTraitDisplay(GameData.WeaponDef weapon, bool owned, int currentLevel)
    {
        if (weapon == null)
            return;

        GameData.WeaponTraitDef firstTrait = owned
            ? GetOrAssignTrait(weapon.weaponId, 5, currentLevel, null)
            : null;
        GameData.WeaponTraitDef secondTrait = owned
            ? GetOrAssignTrait(weapon.weaponId, 10, currentLevel, firstTrait?.traitId)
            : null;

        SetText(traitDetail1Text, firstTrait != null ? firstTrait.name : "Lv. 5 해금");
        SetText(traitDetail2Text, secondTrait != null ? secondTrait.name : "Lv. 10 해금");
        SetTraitTextColor(traitDetail1Text, firstTrait, true);
        SetTraitTextColor(traitDetail2Text, secondTrait, true);

        SetTraitPopupSlot(traitTitle1Text, traitDesc1Text, traitLock1, firstTrait, currentLevel >= 5);
        SetTraitPopupSlot(traitTitle2Text, traitDesc2Text, traitLock2, secondTrait, currentLevel >= 10);
        RefreshTraitRerollControls(weapon, owned, currentLevel, firstTrait, secondTrait);
    }

    void SetTraitPopupSlot(
        TMP_Text title,
        TMP_Text desc,
        GameObject lockObject,
        GameData.WeaponTraitDef trait,
        bool levelReached)
    {
        bool unlocked = levelReached && trait != null;
        if (title != null)
        {
            title.gameObject.SetActive(unlocked);
            if (unlocked)
            {
                SetText(title, trait.name);
                SetTraitTextColor(title, trait, false);
            }
        }
        if (desc != null)
        {
            desc.gameObject.SetActive(unlocked);
            if (unlocked)
            {
                SetText(desc, trait.desc);
                desc.color = Color.black;
            }
        }
        if (lockObject != null)
            lockObject.SetActive(!levelReached);
    }

    void SetTraitTextColor(TMP_Text text, GameData.WeaponTraitDef trait, bool isDetail)
    {
        if (text == null)
            return;

        // 잠금 안내 문구는 기존 회색 박스에서 잘 보이도록 흰색을 유지한다.
        if (trait == null)
        {
            text.color = Color.white;
            return;
        }

        switch ((trait.rarity ?? "").Trim().ToLowerInvariant())
        {
            case "rare":
                text.color = new Color32(0x01, 0x2A, 0xB0, 0xFF);
                break;
            case "epic":
                text.color = new Color32(0x67, 0x00, 0xEA, 0xFF);
                break;
            case "legendary":
                text.color = new Color32(0xB3, 0xBA, 0x00, 0xFF);
                break;
            case "special":
                text.color = Color.red;
                break;
            case "common":
            default:
                text.color = isDetail ? Color.black : Color.white;
                break;
        }
    }

    void RefreshTraitRerollControls(
        GameData.WeaponDef weapon,
        bool owned,
        int currentLevel,
        GameData.WeaponTraitDef firstTrait,
        GameData.WeaponTraitDef secondTrait)
    {
        if (weapon == null)
            return;

        bool firstUnlocked = owned && currentLevel >= 5 && firstTrait != null;
        bool secondUnlocked = owned && currentLevel >= 10 && secondTrait != null;
        bool firstLocked = firstUnlocked && IsTraitLocked(weapon.weaponId, 5);
        bool secondLocked = secondUnlocked && IsTraitLocked(weapon.weaponId, 10);

        RefreshTraitLockButton(traitLock1Button, traitLock1Image, firstUnlocked, firstLocked);
        RefreshTraitLockButton(traitLock2Button, traitLock2Image, secondUnlocked, secondLocked);

        int lockedCount = (firstLocked ? 1 : 0) + (secondLocked ? 1 : 0);
        int rerollCost = 100 + lockedCount * 50;
        bool hasRerollTarget = (firstUnlocked && !firstLocked) || (secondUnlocked && !secondLocked);
        bool canAfford = GameFlowManager.Instance != null &&
                         GameFlowManager.Instance.GetMoney() + 0.0001f >= rerollCost;

        SetButtonText(traitRerollButton, $"리롤(${rerollCost})");
        if (traitRerollButton != null)
            traitRerollButton.interactable = owned && hasRerollTarget && canAfford;
    }

    void RefreshTraitLockButton(Button button, Image image, bool visible, bool locked)
    {
        SetButtonVisible(button, visible);
        if (!visible || image == null)
            return;

        Sprite sprite = locked ? traitLockedSprite : traitUnlockedSprite;
        if (sprite != null)
            image.sprite = sprite;
        image.preserveAspect = true;
    }

    void ToggleTraitLock(int unlockLevel)
    {
        var weapon = CurrentWeapon();
        if (weapon == null || !IsOwned(weapon.weaponId))
            return;

        int currentLevel = GetWeaponLevel(weapon.weaponId);
        if (currentLevel < unlockLevel ||
            GameData.GetWeaponTrait(PlayerPrefs.GetString(TraitKey(Slot, weapon.weaponId, unlockLevel), "")) == null)
            return;

        string key = TraitLockKey(Slot, weapon.weaponId, unlockLevel);
        PlayerPrefs.SetInt(key, IsTraitLocked(weapon.weaponId, unlockLevel) ? 0 : 1);
        SavePrefs();
        Refresh();
    }

    void RerollTraits()
    {
        var weapon = CurrentWeapon();
        if (weapon == null || !IsOwned(weapon.weaponId))
            return;

        int currentLevel = GetWeaponLevel(weapon.weaponId);
        GameData.WeaponTraitDef firstTrait = GetOrAssignTrait(weapon.weaponId, 5, currentLevel, null);
        GameData.WeaponTraitDef secondTrait = GetOrAssignTrait(weapon.weaponId, 10, currentLevel, firstTrait?.traitId);
        bool firstUnlocked = currentLevel >= 5 && firstTrait != null;
        bool secondUnlocked = currentLevel >= 10 && secondTrait != null;
        bool firstLocked = firstUnlocked && IsTraitLocked(weapon.weaponId, 5);
        bool secondLocked = secondUnlocked && IsTraitLocked(weapon.weaponId, 10);
        bool rerollFirst = firstUnlocked && !firstLocked;
        bool rerollSecond = secondUnlocked && !secondLocked;
        if (!rerollFirst && !rerollSecond)
            return;

        int rerollCost = 100 + ((firstLocked ? 1 : 0) + (secondLocked ? 1 : 0)) * 50;
        var flow = GameFlowManager.Instance;
        if (flow == null || !flow.SpendMoney(rerollCost))
        {
            Refresh();
            return;
        }

        GameData.WeaponTraitDef newFirst = firstTrait;
        GameData.WeaponTraitDef newSecond = secondTrait;
        if (rerollFirst)
            newFirst = RollDifferentTrait(weapon.weaponId, firstTrait?.traitId, secondTrait?.traitId) ?? firstTrait;
        if (rerollSecond)
            newSecond = RollDifferentTrait(weapon.weaponId, secondTrait?.traitId, newFirst?.traitId) ?? secondTrait;

        if (rerollFirst && newFirst != null)
            PlayerPrefs.SetString(TraitKey(Slot, weapon.weaponId, 5), newFirst.traitId);
        if (rerollSecond && newSecond != null)
            PlayerPrefs.SetString(TraitKey(Slot, weapon.weaponId, 10), newSecond.traitId);

        SavePrefs();
        flow.SaveCurrentSlot();
        Refresh();
    }

    GameData.WeaponTraitDef RollDifferentTrait(string weaponId, string currentTraitId, string otherTraitId)
    {
        var excluded = new HashSet<string>();
        if (!string.IsNullOrEmpty(currentTraitId)) excluded.Add(currentTraitId);
        if (!string.IsNullOrEmpty(otherTraitId)) excluded.Add(otherTraitId);

        var result = RollWeightedTrait(weaponId, excluded);
        if (result != null)
            return result;

        // 후보가 적으면 중복만 방지하고 기존 특성이 다시 나오는 것은 허용한다.
        excluded.Clear();
        if (!string.IsNullOrEmpty(otherTraitId)) excluded.Add(otherTraitId);
        return RollWeightedTrait(weaponId, excluded);
    }

    GameData.WeaponTraitDef RollWeightedTrait(string weaponId, HashSet<string> excludedTraitIds)
    {
        var candidates = GameData.GetAvailableWeaponTraits(weaponId);
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || excludedTraitIds.Contains(candidate.traitId)) continue;
            totalWeight += Mathf.Max(0, candidate.weight);
        }
        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || excludedTraitIds.Contains(candidate.traitId)) continue;
            roll -= Mathf.Max(0, candidate.weight);
            if (roll < 0)
                return candidate;
        }
        return null;
    }

    bool IsTraitLocked(string weaponId, int unlockLevel)
    {
        return PlayerPrefs.GetInt(TraitLockKey(Slot, weaponId, unlockLevel), 0) == 1;
    }

    GameData.WeaponTraitDef GetOrAssignTrait(
        string weaponId,
        int unlockLevel,
        int currentLevel,
        string excludedTraitId)
    {
        string key = TraitKey(Slot, weaponId, unlockLevel);
        string savedTraitId = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(savedTraitId))
            return GameData.GetWeaponTrait(savedTraitId);

        if (currentLevel < unlockLevel)
            return null;

        var candidates = GameData.GetAvailableWeaponTraits(weaponId);
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate.traitId == excludedTraitId) continue;
            totalWeight += Mathf.Max(0, candidate.weight);
        }
        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate.traitId == excludedTraitId) continue;
            roll -= Mathf.Max(0, candidate.weight);
            if (roll >= 0) continue;

            PlayerPrefs.SetString(key, candidate.traitId);
            SavePrefs();
            return candidate;
        }

        return null;
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

    void BindLevelUpContent()
    {
        var root = FindChildTransform("levelUpContent", "upgradeContent", "enhanceContent");
        levelUpContent = root != null ? root.gameObject : null;
        if (root == null) return;

        levelText = FindTmpIn(root, "level", "levelText", "levelTitle");
        successRateText = FindTmpIn(root, "successRate", "success", "successText");
        failRateText = FindTmpIn(root, "failRate", "fail", "failText");
        breakRateText = FindTmpIn(root, "breakRate", "break", "breakText", "destroyRate");
        upgradeCostText = FindTmpIn(root, "cost", "upgradeCost", "costText");
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
            valueText.color = !hasData ? Color.white : (currentAmount >= cost.amount ? new Color32(0x00, 0xC8, 0x16, 0xFF) : new Color(1f, 0.35f, 0.35f, 1f));
        }
    }

    void RefreshLevelUpContent(GameData.WeaponDef weapon, bool owned, int currentLevel, GameData.WeaponUpgradeDef upgrade)
    {
        if (levelUpContent == null || weapon == null) return;
        levelUpContent.SetActive(owned);
        if (!owned) return;

        bool hasNextUpgrade = CanUseUpgrade(upgrade, currentLevel);
        SetText(levelText, FormatLevelText(weapon.weaponId, currentLevel, upgrade));
        SetText(upgradeCostText, hasNextUpgrade ? $"강화 비용 : ${upgrade.upgradeCost:0.#}" : "강화 비용 : -");
        SetText(successRateText, hasNextUpgrade ? $"<color=#05C828>성공</color> : {upgrade.successRate:0.#}%" : "<color=#05C828>성공</color> : -");
        SetText(failRateText, hasNextUpgrade ? $"<color=#FF0F02>실패</color> : {upgrade.failRate:0.#}%" : "<color=#FF0F02>실패</color> : -");
        SetText(breakRateText, hasNextUpgrade ? $"<color=#620198>파괴</color> : {upgrade.breakRate:0.#}%" : "<color=#620198>파괴</color> : -");
    }

    string FormatLevelText(string weaponId, int currentLevel, GameData.WeaponUpgradeDef upgrade)
    {
        if (upgrade == null)
            return "Lv. MAX";

        bool reachesMaxLevel = upgrade.nextLevelIsMax ||
                               GameData.GetWeaponUpgrade(weaponId, upgrade.nextLevel) == null;
        string nextText = reachesMaxLevel ? "MAX" : upgrade.nextLevel.ToString();
        return $"Lv. {currentLevel} -> Lv. {nextText}";
    }

    string FormatUpgradeDelta(float value, string format)
    {
        if (Mathf.Approximately(value, 0f))
            return "";

        string sign = value > 0f ? "+" : "-";
        string amount = Mathf.Abs(value).ToString(format);
        return $" <alpha=#80>( {sign} {amount} )";
    }

    bool CanUseUpgrade(GameData.WeaponUpgradeDef upgrade, int currentLevel)
    {
        return upgrade != null &&
               !upgrade.nextLevelIsMax &&
               upgrade.nextLevel > currentLevel;
    }

    static int ClampWeaponLevel(string weaponId, int level)
    {
        return Mathf.Clamp(level, DefaultWeaponLevel, GetMaxWeaponLevel(weaponId));
    }

    static int GetMaxWeaponLevel(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
            return DefaultWeaponLevel;

        int level = DefaultWeaponLevel;
        for (int guard = 0; guard < 100; guard++)
        {
            var upgrade = GameData.GetWeaponUpgrade(weaponId, level);
            if (upgrade == null || upgrade.nextLevelIsMax || upgrade.nextLevel <= level)
                break;

            level = upgrade.nextLevel;
        }

        return Mathf.Max(DefaultWeaponLevel, level);
    }

    static int GetPreviousWeaponLevel(string weaponId, int currentLevel)
    {
        currentLevel = ClampWeaponLevel(weaponId, currentLevel);
        int previous = DefaultWeaponLevel;
        int maxLevel = GetMaxWeaponLevel(weaponId);

        for (int level = DefaultWeaponLevel; level <= maxLevel; level++)
        {
            var upgrade = GameData.GetWeaponUpgrade(weaponId, level);
            if (upgrade == null || upgrade.nextLevelIsMax)
                continue;

            if (upgrade.nextLevel == currentLevel)
                previous = Mathf.Max(previous, upgrade.level);
        }

        if (previous < currentLevel)
            return previous;

        return Mathf.Max(DefaultWeaponLevel, currentLevel - 1);
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

    void UpgradeCurrent()
    {
        var weapon = CurrentWeapon();
        if (weapon == null || !IsOwned(weapon.weaponId)) return;

        int currentLevel = GetWeaponLevel(weapon.weaponId);
        var upgrade = GameData.GetWeaponUpgrade(weapon.weaponId, currentLevel);
        if (!CanUseUpgrade(upgrade, currentLevel)) return;

        var flow = GameFlowManager.Instance;
        if (flow == null || !flow.SpendMoney(upgrade.upgradeCost))
        {
            Refresh();
            return;
        }

        float totalRate = Mathf.Max(0f, upgrade.successRate + upgrade.failRate + upgrade.breakRate);
        float roll = totalRate > 0f ? Random.Range(0f, totalRate) : 0f;
        if (roll < upgrade.successRate)
        {
            SetWeaponLevel(weapon.weaponId, upgrade.nextLevel);
        }
        else if (roll >= upgrade.successRate + upgrade.failRate && upgrade.breakRate > 0f)
        {
            SetWeaponLevel(weapon.weaponId, DefaultWeaponLevel);
        }
        else
        {
            SetWeaponLevel(weapon.weaponId, GetPreviousWeaponLevel(weapon.weaponId, currentLevel));
        }

        SavePrefs();
        flow.SaveCurrentSlot();
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

    static void GetEffectiveStats(GameData.WeaponDef weapon, int level, out int damage, out float fireInterval)
    {
        damage = weapon != null ? weapon.damage : 1;
        fireInterval = weapon != null ? weapon.fireInterval : 1f;
        if (weapon == null) return;

        GameData.GetWeaponUpgradeTotals(weapon.weaponId, level, out int damageAdd, out float fireIntervalAdd);
        damage = Mathf.Max(1, damage + damageAdd);
        fireInterval = Mathf.Max(0.05f, fireInterval - fireIntervalAdd);
    }

    public static int GetEffectiveDamage(GameData.WeaponDef weapon)
    {
        if (weapon == null) return 1;
        GetEffectiveStats(weapon, GetWeaponLevel(weapon.weaponId), out int damage, out _);
        return damage;
    }

    public static float GetEffectiveFireInterval(GameData.WeaponDef weapon)
    {
        if (weapon == null) return 1f;
        GetEffectiveStats(weapon, GetWeaponLevel(weapon.weaponId), out _, out float fireInterval);
        return fireInterval;
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

    Button FindButtonIn(Transform root, params string[] names)
    {
        Transform target = FindChildTransformIn(root, names);
        if (target == null) return null;
        var button = target.GetComponent<Button>();
        if (button == null) button = target.gameObject.AddComponent<Button>();
        return button;
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
        if (text == null) return;
        text.richText = true;
        text.text = value;
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
    static string LevelKey(int slot, string weaponId) => $"slot_{slot}_weapon_level_{weaponId}";
    static string TraitKey(int slot, string weaponId, int unlockLevel) =>
        $"slot_{slot}_weapon_trait_{weaponId}_{unlockLevel}";
    static string TraitLockKey(int slot, string weaponId, int unlockLevel) =>
        $"slot_{slot}_weapon_trait_lock_{weaponId}_{unlockLevel}";

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

    public static int GetWeaponLevel(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId) || Slot < 1) return DefaultWeaponLevel;
        return ClampWeaponLevel(weaponId, PlayerPrefs.GetInt(LevelKey(Slot, weaponId), DefaultWeaponLevel));
    }

    public static void SetWeaponLevel(string weaponId, int level)
    {
        if (string.IsNullOrEmpty(weaponId) || Slot < 1) return;
        PlayerPrefs.SetInt(LevelKey(Slot, weaponId), ClampWeaponLevel(weaponId, level));
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
            PlayerPrefs.DeleteKey(LevelKey(slot, list[i].weaponId));
            PlayerPrefs.DeleteKey(TraitKey(slot, list[i].weaponId, 5));
            PlayerPrefs.DeleteKey(TraitKey(slot, list[i].weaponId, 10));
            PlayerPrefs.DeleteKey(TraitLockKey(slot, list[i].weaponId, 5));
            PlayerPrefs.DeleteKey(TraitLockKey(slot, list[i].weaponId, 10));
        }
    }

    public static void ResetSlotWeaponLevels(int slot)
    {
        if (slot < 1) return;
        var list = GameData.GetWeapons();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || string.IsNullOrEmpty(list[i].weaponId)) continue;
            PlayerPrefs.SetInt(LevelKey(slot, list[i].weaponId), DefaultWeaponLevel);
            PlayerPrefs.DeleteKey(TraitKey(slot, list[i].weaponId, 5));
            PlayerPrefs.DeleteKey(TraitKey(slot, list[i].weaponId, 10));
            PlayerPrefs.DeleteKey(TraitLockKey(slot, list[i].weaponId, 5));
            PlayerPrefs.DeleteKey(TraitLockKey(slot, list[i].weaponId, 10));
        }
        PlayerPrefs.Save();
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
