using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }
    private PhotonView photonView;

    [Header("UI設定")]
    public GameObject powerUpCanvas;
    public GameObject player1ChoicePanel, player2ChoicePanel;
    public PowerUpButton[] player1Buttons, player2Buttons;

    [Header("能力リスト")]
    public List<PowerUpData> allPowerUps;

    // 選択状況を管理する変数（マスタークライアントのみ使用）
    private Player p1, p2;
    private int p1ChoiceDataIndex = -1, p2ChoiceDataIndex = -1;
    private int[] p1OptionIndices, p2OptionIndices;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        photonView = GetComponent<PhotonView>();
    }

    // NetworkGameManagerから呼ばれる開始関数（マスタークライアントのみ）
    public void StartPowerUpSelectionProcess(Player victim, Player attacker)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        p1ChoiceDataIndex = -1;
        p2ChoiceDataIndex = -1;

        // プレイヤー情報を取得
        p1 = PhotonNetwork.PlayerList[0];
        p2 = (PhotonNetwork.PlayerList.Length > 1) ? PhotonNetwork.PlayerList[1] : null;


        // ★★★ レアリティに基づいて抽選し、提示する能力を決定 ★★★
        p1OptionIndices = GetPowerUpsByRarity(3);
        p2OptionIndices = GetPowerUpsByRarity(3);

        // 念のため、抽選結果がnullでないかチェック
        if (p1OptionIndices == null || p2OptionIndices == null)
        {
            Debug.LogError("能力の抽選に失敗しました。allPowerUpsリストが空でないか確認してください。");
            return;
        }

        photonView.RPC(nameof(Rpc_ShowChoiceUI), RpcTarget.All, p1OptionIndices, p2OptionIndices, victim);
    }

    private int[] GetPowerUpsByRarity(int count) //レアリティー
    {
        if (allPowerUps == null || allPowerUps.Count == 0) return null;

        List<int> chosenIndices = new List<int>();
        List<int> availableIndices = Enumerable.Range(0, allPowerUps.Count).ToList();

        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break;

            float randomValue = Random.value; // 0.0f 〜 1.0f のランダムな値
            Rarity chosenRarity;

            // 確率に基づいてレアリティを決定
            if (randomValue < 0.05f) // 5%の確率
                chosenRarity = Rarity.Epic;
            else if (randomValue < 0.20f) // 15%の確率 (5%〜20%)
                chosenRarity = Rarity.Rare;
            else // 80%の確率
                chosenRarity = Rarity.Common;

            // 決定したレアリティの能力だけをリストアップ
            List<int> candidates = availableIndices.Where(index => allPowerUps[index].rarity == chosenRarity).ToList();

            // もしそのレアリティの能力がなければ、一つ下のレアリティで再抽選
            if (candidates.Count == 0)
            {
                if (chosenRarity == Rarity.Epic) chosenRarity = Rarity.Rare;
                candidates = availableIndices.Where(index => allPowerUps[index].rarity == chosenRarity).ToList();
            }
            if (candidates.Count == 0)
            {
                if (chosenRarity == Rarity.Rare) chosenRarity = Rarity.Common;
                candidates = availableIndices.Where(index => allPowerUps[index].rarity == chosenRarity).ToList();
            }
            if (candidates.Count == 0) continue; // それでもなければ諦める

            // 候補の中からランダムに1つ選ぶ
            int randomIndex = Random.Range(0, candidates.Count);
            int chosenIndex = candidates[randomIndex];

            chosenIndices.Add(chosenIndex);
            availableIndices.Remove(chosenIndex); // 一度選ばれたものは候補から外す
        }
        return chosenIndices.ToArray();
    }

    [PunRPC]
    private void Rpc_ShowChoiceUI(int[] p1_indices, int[] p2_indices, Player victim)
    {
        // 自分のPlayerInputコンポーネントを探して無効化する
        foreach (var pc in FindObjectsByType<NewPlayerController>(FindObjectsSortMode.None))
        {
            if (pc.GetComponent<PhotonView>().IsMine)
            {
                pc.GetComponent<PlayerInput>().enabled = false;
                break;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetupPanel(player1Buttons, p1_indices, 1);
        SetupPanel(player2Buttons, p2_indices, 2);

        powerUpCanvas.SetActive(true);

        Player localPlayer = PhotonNetwork.LocalPlayer;
        bool isLocalPlayerP1 = localPlayer.ActorNumber == PhotonNetwork.PlayerList[0].ActorNumber;
        bool isLocalPlayerP2 = localPlayer.ActorNumber == PhotonNetwork.PlayerList[1].ActorNumber;

        // プレイヤー1には player1ChoicePanel を、プレイヤー2には player2ChoicePanel を表示
        if (isLocalPlayerP1)
        {
            player1ChoicePanel.SetActive(true);
            player2ChoicePanel.SetActive(false);
        }
        else if (isLocalPlayerP2)
        {
            player1ChoicePanel.SetActive(false);
            player2ChoicePanel.SetActive(true);
        }
        else
        {
            // どちらでもない場合は両方非表示（観戦者など）
            player1ChoicePanel.SetActive(false);
            player2ChoicePanel.SetActive(false);
        }
    }

    // ボタンから呼ばれ、マスタークライアントに選択を報告する
    public void ReportChoice(int playerNumber, int choiceIndexInPanel)
    {
        // ★★★ 自分のプレイヤー番号と一致する場合のみ画面を非表示にする ★★★
        Player localPlayer = PhotonNetwork.LocalPlayer;
        bool isLocalPlayerP1 = localPlayer.ActorNumber == PhotonNetwork.PlayerList[0].ActorNumber;

        if (playerNumber == 1 && isLocalPlayerP1)
        {
            player1ChoicePanel.SetActive(false);
        }
        else if (playerNumber == 2 && !isLocalPlayerP1)
        {
            player2ChoicePanel.SetActive(false);
        }

        // マスタークライアントに選択結果を送信
        photonView.RPC(nameof(Rpc_SubmitChoice), RpcTarget.MasterClient, playerNumber, choiceIndexInPanel);
    }

    [PunRPC]
    private void Rpc_SubmitChoice(int playerNumber, int choiceIndexInPanel, PhotonMessageInfo info)
    {
        // このRPCはマスタークライアントだけが実行する
        if (!PhotonNetwork.IsMasterClient) return;

        // 送信者が正しいプレイヤーか念のため確認
        if (info.Sender.ActorNumber != PhotonNetwork.PlayerList[playerNumber - 1].ActorNumber) return;

        bool isP1Choosing = (playerNumber == 1);

        if (isP1Choosing)
        {
            p1ChoiceDataIndex = p1OptionIndices[choiceIndexInPanel];
        }
        else
        {
            p2ChoiceDataIndex = p2OptionIndices[choiceIndexInPanel];
        }

        // 両者が選択を終えたかチェック
        if (p1ChoiceDataIndex != -1 && p2ChoiceDataIndex != -1)
        {
            // 全員にゲーム再開を命令
            photonView.RPC(nameof(Rpc_ApplyChoicesAndResume), RpcTarget.All, p1ChoiceDataIndex, p2ChoiceDataIndex);
        }
    }

    /*

    [PunRPC]
    private void Rpc_ActivatePanel(int playerNumberToActivate)
    {
        // すでに両者が選び終わっている場合は何もしない
        if (p1ChoiceDataIndex != -1 && p2ChoiceDataIndex != -1) return;

        player1ChoicePanel.SetActive(playerNumberToActivate == 1);
        player2ChoicePanel.SetActive(playerNumberToActivate == 2);
    }

    */

    [PunRPC]
    private void Rpc_ApplyChoicesAndResume(int p1_finalDataIndex, int p2_finalDataIndex)
    {
        GameObject p1_go = FindPlayerObject(PhotonNetwork.PlayerList[0]);
        GameObject p2_go = FindPlayerObject(PhotonNetwork.PlayerList[1]);

        ApplyEffectToPlayer(p1_go, allPowerUps[p1_finalDataIndex]);
        ApplyEffectToPlayer(p2_go, allPowerUps[p2_finalDataIndex]);

        powerUpCanvas.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 自分のPlayerInputを再度有効化
        foreach (var pc in FindObjectsByType<NewPlayerController>(FindObjectsSortMode.None))
        {
            if (pc.GetComponent<PhotonView>().IsMine)
            {
                pc.GetComponent<PlayerInput>().enabled = true;
                break;
            }
        }

        if (PhotonNetwork.IsMasterClient && NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.RespawnPlayers();
    }

    // --- (以下の補助関数は変更なし) ---
    #region Helper_Functions
    private int[] GetRandomPowerUpIndices(int count)
    {
        return Enumerable.Range(0, allPowerUps.Count).OrderBy(x => Random.value).Take(count).ToArray();
    }

    private void SetupPanel(PowerUpButton[] buttons, int[] indices, int playerNumber)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Setup(allPowerUps[indices[i]]);
            int choiceIndex = i;
            buttons[i].button.onClick.RemoveAllListeners();
            buttons[i].button.onClick.AddListener(() => ReportChoice(playerNumber, choiceIndex));
        }
    }

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

    private void ApplyEffectToPlayer(GameObject player, PowerUpData data)
    {
        if (player == null || data == null) return;
        NewPlayerController pc = player.GetComponent<NewPlayerController>();
        if (pc == null) return;

        switch (data.powerUpType)
        {
            case PowerUpType.MagicSpeedUp: pc.magicSpeed *= data.value; break;
            case PowerUpType.MagicDamageUp: pc.bonusDamage += (int)data.value; break;
            case PowerUpType.ProjectileBounce:
                pc.projectileCanBounce = true;
                break;

            case PowerUpType.DoubleJump:
                pc.canDoubleJump = true;
                break;
            case PowerUpType.ExplosiveShot:
                pc.hasExplosiveShot = true;
                // オプション: data.value を使って爆発範囲やダメージを可変にする
                // pc.explosionRadius = data.value;
                break;
            case PowerUpType.LifeSteal:
                // data.value を吸収率として設定 (例: 0.3)
                pc.lifeStealRatio += data.value;
                break;
            case PowerUpType.Dash:
                pc.canDash = true;
                break;
        }
    }
    #endregion
}