using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class PowerUpButton : MonoBehaviour
{
    // Inspectorで設定するUI要素
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] public Button button; // PowerUpManagerからアクセスするためpublicに

    [SerializeField] private Image backgroundImage; // 背景画像への参照を追加

    // ゲーム起動時とUnityエディタでの変更時に、参照が正しいか自動で検証・修復する
    private void OnValidate()
    {
        if (button == null) button = GetComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

        // 子オブジェクトを名前で検索して、自動的に参照を設定する
        if (nameText == null)
        {
            Transform foundText = transform.Find("Name");
            if (foundText != null) nameText = foundText.GetComponent<TextMeshProUGUI>();
        }
        if (descriptionText == null)
        {
            Transform foundText = transform.Find("Description");
            if (foundText != null) descriptionText = foundText.GetComponent<TextMeshProUGUI>();
        }
    }

    // このボタンに能力データを設定する
    public void Setup(PowerUpData data)
    {
        if (data == null)
        {
            Debug.LogError($"【エラー】{gameObject.name} に設定しようとしたPowerUpDataがnullです。");
            return;
        }

        if (nameText != null)
        {
            nameText.text = data.powerUpName;
            // ★★★ レアリティに応じてテキストの色を変更 ★★★
            nameText.color = GetColorForRarity(data.rarity);
        }
        else
        {
            Debug.LogError($"【エラー】{gameObject.name} の nameText が設定されていません！");
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        if (backgroundImage != null)
        {
            // ★★★ レアリティに応じて背景色を少しだけ変える ★★★
            // (完全に色を変える場合は Color.Lerp を使わずに直接色を指定)
            backgroundImage.color = Color.Lerp(Color.white, GetColorForRarity(data.rarity), 0.25f);
        }
    }

    private Color GetColorForRarity(Rarity rarity) //レアリティーの色
    {
        switch (rarity)
        {
            case Rarity.Common:
                return Color.black; // コモンは黒文字
            case Rarity.Rare:
                return new Color(0.1f, 0.4f, 0.9f); // レアは青色 (RGB: 0.1, 0.4, 0.9)
            case Rarity.Epic:
                return new Color(0.6f, 0.2f, 0.8f); // エピックは紫色 (RGB: 0.6, 0.2, 0.8)
            default:
                return Color.grey;
        }
    }
}
