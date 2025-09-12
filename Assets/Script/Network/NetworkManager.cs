using UnityEngine;
using Photon.Pun; // PUN 2を使うために必要
using Photon.Realtime; // PUN 2を使うために必要

// MonoBehaviourPunCallbacks を継承すると、Photonの様々なイベントを自動で受け取れる
public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("プレイヤーのプレハブ")]
    public GameObject playerPrefab; // InspectorでPlayerプレハブを設定

    [Header("リスポーン地点")]
    public Transform[] spawnPoints; // Inspectorでスポーン地点を複数設定
    void Start()
    {
        Debug.Log("サーバーに接続しています...");
        // ゲームバージョンを設定（重要：異なるバージョンのプレイヤーとはマッチングしなくなる）
        PhotonNetwork.GameVersion = "1.0";
        // Photonサーバーに接続する
        PhotonNetwork.ConnectUsingSettings();
    }

    // サーバーへの接続が成功した時に呼ばれる
    public override void OnConnectedToMaster()
    {
        Debug.Log("サーバー接続成功！");
        Debug.Log("ルームを探しています...");
        // ランダムなルームに参加する。もし誰もいなければ、新しいルームを作成して参加する
        PhotonNetwork.JoinRandomOrCreateRoom();
    }

    // サーバーへの接続が失敗した時に呼ばれる
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError("サーバー接続失敗: " + cause);
    }

    // ルームへの参加が成功した時に呼ばれる
    public override void OnJoinedRoom()
    {
        Debug.Log("ルーム参加成功！");
        Debug.Log("ルーム名: " + PhotonNetwork.CurrentRoom.Name);
        Debug.Log("プレイヤー数: " + PhotonNetwork.CurrentRoom.PlayerCount);

        // ★★★ここに、ルームに参加した後のプレイヤー生成処理などを追加していく★★★
        // プレイヤー数を元に、使用するスポーン地点を決定
        int playerIndex = PhotonNetwork.CurrentRoom.PlayerCount - 1;
        Transform spawnPoint = spawnPoints[playerIndex % spawnPoints.Length]; // プレイヤー数に応じてスポーン地点を循環させる

        // プレイヤーをネットワークオブジェクトとして生成
        if (playerPrefab != null)
        {
            PhotonNetwork.Instantiate(playerPrefab.name, spawnPoint.position, spawnPoint.rotation);
            Debug.Log("プレイヤーを生成しました。");
        }
        else
        {
            Debug.LogError("Player Prefabが設定されていません！");
        }
    }

    // ルームへの参加が失敗した時に呼ばれる
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("ランダムなルームへの参加に失敗しました。新しいルームを作成します。");
        // 失敗した場合、自分で新しいルームを作成する
        PhotonNetwork.CreateRoom(null, new RoomOptions());
    }
}