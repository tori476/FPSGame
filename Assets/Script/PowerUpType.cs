using UnityEngine;

// PowerUpの種類を定義する列挙型
public enum PowerUpType
{
    MagicSpeedUp,    // 魔法の速度アップ
    MagicDamageUp,   // 魔法のダメージアップ
    HealthUp,        // 体力アップ
    MoveSpeedUp,     // 移動速度アップ
    JumpForceUp,      // ジャンプ力アップ
    ProjectileBounce, // 弾が反射する能力
    DoubleJump,       // 二段ジャンプが可能になる能力

    ExplosiveShot, // 魔法が着弾時に爆発

    LifeSteal // ダメージの一部を吸収
    // TODO: ここに新しい能力の種類を追加していく
}

// ★★★ レアリティを定義する列挙型を追加 ★★★
public enum Rarity
{
    Common,     // コモン（一般的）
    Rare,       // レア
    Epic        // エピック（叙事詩級）
}

// ScriptableObjectとして能力データを作成できるようにする
[CreateAssetMenu(fileName = "NewPowerUp", menuName = "Game/PowerUp Data")]
public class PowerUpData : ScriptableObject
{
    [Header("基本情報")]
    public string powerUpName = "新しい能力";
    [TextArea(2, 4)]
    public string description = "能力の説明文";

    [Header("効果設定")]
    public PowerUpType powerUpType;
    public float value = 1.5f; // 効果の値（倍率や追加値）

    // ★★★ レアリティの項目を追加 ★★★
    [Header("レアリティ")]
    public Rarity rarity = Rarity.Common;

    [Header("UI表示用（オプション）")]
    public Sprite icon;
    // public Color backgroundColor = Color.white; // 色はレアリティから自動で決定するためコメントアウト
}