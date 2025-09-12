using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// PhotonViewが必須
[RequireComponent(typeof(PhotonView))]
public class Health : MonoBehaviour, IPunObservable // IPunObservableを追加
{
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;
    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        currentHealth = maxHealth;
    }

    // ダメージを受ける処理（これは誰でも呼び出せる）
    public void TakeDamage(int damage, Photon.Realtime.Player attacker)
    {
        // ダメージ処理の実行は、マスタークライアントにRPCで依頼する
        photonView.RPC("Rpc_TakeDamage", RpcTarget.MasterClient, damage, attacker);
    }

    // 実際のダメージ処理（マスタークライアントのみが実行）
    [PunRPC]
    private void Rpc_TakeDamage(int damage, Photon.Realtime.Player attacker)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} が {damage} ダメージを受けた！ 残りHP: {currentHealth}");

        // ★★★ ライフスティール処理（マスタークライアントのみ） ★★★
        GameObject attackerObject = FindPlayerObject(attacker);
        if (attackerObject != null)
        {
            NewPlayerController attackerController = attackerObject.GetComponent<NewPlayerController>();
            if (attackerController.lifeStealRatio > 0)
            {
                int healAmount = Mathf.FloorToInt(damage * attackerController.lifeStealRatio);
                attackerObject.GetComponent<PhotonView>().RPC("Rpc_Heal", attacker, healAmount);
            }
        }

        if (currentHealth <= 0)
        {
            // NetworkGameManagerに死亡を通知
            NetworkGameManager.Instance.OnPlayerDied(photonView.Owner, attacker);
        }
    }

    // プレイヤーオブジェクトを探すヘルパー関数（PowerUpManagerから拝借）
    private GameObject FindPlayerObject(Player player)
    {
        foreach (var pView in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
        {
            if (pView.OwnerActorNr == player.ActorNumber && pView.CompareTag("Player"))
            {
                return pView.gameObject;
            }
        }
        return null;
    }

    // HPをリセットする処理
    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    // HPの値をネットワークで同期するための処理
    void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 自分のHP情報を送信
            stream.SendNext(currentHealth);
        }
        else
        {
            // 他のプレイヤーのHP情報を受信
            this.currentHealth = (int)stream.ReceiveNext();
        }
    }

    public void Heal(int amount)
    {
        // 回復処理は自分のクライアントで実行して、HPの同期に任せる
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    [PunRPC]
    private void Rpc_Heal(int amount)
    {
        Heal(amount);
    }
}