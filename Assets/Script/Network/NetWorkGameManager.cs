using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

[RequireComponent(typeof(PhotonView))]
public class NetworkGameManager : MonoBehaviourPunCallbacks
{
    public static NetworkGameManager Instance { get; private set; }
    //private PhotonView photonView;

    [Header("ゲーム設定")]
    public int winningScore = 5;

    // スポーン地点の情報をNetworkManagerからここに移動
    [Header("リスポーン地点")]
    public Transform[] spawnPoints;

    private Dictionary<Player, int> playerScores = new Dictionary<Player, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        //photonView = GetComponent<PhotonView>();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeScoreForPlayer(newPlayer);
        }
    }

    private void InitializeScoreForPlayer(Player player)
    {
        if (!playerScores.ContainsKey(player))
        {
            playerScores[player] = 0;
            photonView.RPC(nameof(Rpc_UpdateScoreUI), RpcTarget.All, player, 0);
        }
    }

    // プレイヤーが倒された時にHealthから呼ばれる（マスタークライアント上でのみ）
    public void OnPlayerDied(Player victim, Player attacker)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (!playerScores.ContainsKey(attacker)) InitializeScoreForPlayer(attacker);
        playerScores[attacker]++;

        photonView.RPC(nameof(Rpc_UpdateScoreUI), RpcTarget.All, attacker, playerScores[attacker]);

        if (playerScores[attacker] >= winningScore)
        {
            photonView.RPC(nameof(Rpc_EndGame), RpcTarget.All, attacker);
        }
        else
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.StartPowerUpSelectionProcess(victim, attacker);
            }
        }
    }

    // --- ▼ リスポーン処理をここに追加 ▼ ---
    public void RespawnPlayers()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(Rpc_RespawnAllPlayers), RpcTarget.All);
    }

    [PunRPC]
    private void Rpc_RespawnAllPlayers()
    {
        NewPlayerController[] players = FindObjectsByType<NewPlayerController>(FindObjectsSortMode.None);
        foreach (var playerController in players)
        {
            // ★★★ IsMineではなく、所有者の情報でリスポーンさせる ★★★
            PhotonView pv = playerController.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null)
            {
                // プレイヤー番号 (1 or 2) からスポーン地点を決定
                int playerIndex = pv.Owner.ActorNumber - 1;
                Transform spawnPoint = spawnPoints[playerIndex % spawnPoints.Length];

                var cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = spawnPoint.position;
                playerController.transform.rotation = spawnPoint.rotation;
                if (cc != null) cc.enabled = true;

                playerController.GetComponent<Health>().ResetHealth();
                Debug.Log($"{playerController.gameObject.name} をリスポーンしました。");
            }
        }
    }
    // --- ▲ リスポーン処理をここに追加 ▲ ---


    [PunRPC]
    private void Rpc_UpdateScoreUI(Player player, int newScore)
    {
        Debug.Log($"[スコア更新] {player.NickName}: {newScore}点");
    }

    [PunRPC]
    private void Rpc_EndGame(Player winner)
    {
        Debug.Log($"ゲーム終了！ 勝者: {winner.NickName}");
    }
}