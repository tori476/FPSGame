using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class NewPlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    private PhotonView photonView;
    private bool isCursorLocked = true;

    [Header("移動・視点操作")]
    public float moveSpeed = 5.0f;
    public float jumpForce = 8.0f;
    public float lookSpeed = 0.5f;
    public float lookXLimit = 80.0f;

    [Header("魔法")]
    public GameObject magicPrefab;
    public Transform magicSpawnPoint;
    public float magicSpeed = 30f;

    [Header("着地判定 (Raycast)")]
    public Transform groundCheckPoint;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.2f;

    [Header("参照")]
    public Camera playerCamera;

    // ★ クロスヘアの参照を保持するプライベート変数を追加
    private Image crosshairImage;


    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private float gravity = 20.0f;

    // 入力値を保持する変数
    private Vector2 moveInput;
    private Vector2 lookInput;

    private bool isGrounded;
    private bool canFire = true;

    [Header("能力")]
    public int bonusDamage = 0;

    [Header("取得した能力")]

    public List<PowerUpType> acquiredPowerUps = new List<PowerUpType>(); //取得したかとある能力のリスト
    public bool canDoubleJump = false;    // 二段ジャンプがアンロックされたか
    public bool projectileCanBounce = false; // 弾が反射するか

    // ...
    public bool hasExplosiveShot = false; // 魔法が爆発するか
    public float explosionRadius = 3.0f; // 爆発範囲
    public int explosionDamage = 15;   // 爆発ダメージ

    // ...

    public float lifeStealRatio = 0.3f; // 吸収率 (例: 0.3f なら30%)

    // ...
    public bool canDash = false;
    private float dashSpeed = 25f;
    private float dashDuration = 0.15f;
    private float dashCooldown = 3.0f;
    private float lastDashTime = -99f;


    // 二段ジャンプ用の状態変数
    private bool hasDoubleJumped = false;

    // 射撃のクールダウンを追加（重複発射防止）
    private float lastFireTime = 0f;
    private float fireRate = 0.3f; // 秒間の最大発射間隔

    // ネットワーク同期用の変数
    private float networkRotationX = 0f;
    private float networkRotationY = 0f;


    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        photonView = GetComponent<PhotonView>();

        // 自分のプレイヤーかどうかを判定
        if (photonView.IsMine)
        {
            // 自分のキャラクターなら、タグを使ってシーン上のクロスヘアUIを探して有効にする
            GameObject crosshairObject = GameObject.FindWithTag("CrosshairUI");
            if (crosshairObject != null)
            {
                crosshairImage = crosshairObject.GetComponent<Image>();
                if (crosshairImage != null)
                {
                    crosshairImage.gameObject.SetActive(true);
                }
            }
            else
            {
                // 見つからなかった場合に警告を出す
                Debug.LogWarning("タグ 'CrosshairUI' がついたクロスヘアが見つかりません。");
            }
        }
        else
        {
            // 他のプレイヤーなら、カメラと操作を無効にする
            playerCamera.gameObject.SetActive(false);
            enabled = false;
        }

        // カーソルロックの初期設定
        if (isCursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Start()
    {
        if (photonView.IsMine)
        {
            ToggleCursor(true);
        }


    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 自分の視点情報を送信
            stream.SendNext(rotationX);
            stream.SendNext(transform.rotation.eulerAngles.y);
        }
        else
        {
            // 他プレイヤーの視点情報を受信
            networkRotationX = (float)stream.ReceiveNext();
            networkRotationY = (float)stream.ReceiveNext();
        }
    }

    public void OnToggleCursor(InputAction.CallbackContext value)
    {
        if (!photonView.IsMine) return;

        if (value.performed)
        {
            ToggleCursor(!isCursorLocked);
        }
    }

    private void ToggleCursor(bool shouldBeLocked)
    {
        isCursorLocked = shouldBeLocked;
        if (isCursorLocked)
        {
            Debug.Log("カーソルをロックします。");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Debug.Log("カーソルのロックを解除します。");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void OnMove(InputAction.CallbackContext value)
    {
        moveInput = value.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext value)
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        lookInput = value.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext value)
    {
        if (!photonView.IsMine || !value.performed) return;

        // 地上にいる場合は、通常のジャンプ
        if (isGrounded)
        {
            moveDirection.y = jumpForce;
            hasDoubleJumped = false; // 地上にいるのでダブルジャンプの状態をリセット
        }
        // 空中にいて、ダブルジャンプ能力があり、まだダブルジャンプを使っていない場合
        else if (canDoubleJump && !hasDoubleJumped)
        {
            moveDirection.y = jumpForce; // 再度ジャンプ力を与える
            hasDoubleJumped = true;      // ダブルジャンプを使用したことを記録
        }
    }

    public void OnFire(InputAction.CallbackContext value)
    {
        if (!photonView.IsMine || !value.performed || !canFire) return;

        // クールダウンチェック（連続射撃防止）
        if (Time.time - lastFireTime < fireRate) return;

        lastFireTime = Time.time;

        // 弾の生成位置と回転を計算（カメラの位置ではなく、プレイヤーの前方から）
        Vector3 spawnPosition = CalculateProjectileSpawnPosition();
        Quaternion spawnRotation = CalculateProjectileSpawnRotation();

        // ネットワークオブジェクトとして魔法を生成
        GameObject projectile = PhotonNetwork.Instantiate(
            magicPrefab.name,
            spawnPosition,
            spawnRotation
        );


    }

    // 弾の生成位置を計算（カメラに依存しない）
    private Vector3 CalculateProjectileSpawnPosition()
    {
        if (magicSpawnPoint != null)
        {
            return magicSpawnPoint.position;
        }

        // magicSpawnPointが設定されていない場合の代替処理
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        // プレイヤーの前方1.5m、上0.5mの位置から発射
        return transform.position + forward * 1.5f + up * 1.0f;
    }

    // 弾の発射方向を計算
    private Quaternion CalculateProjectileSpawnRotation()
    {
        if (photonView.IsMine && playerCamera != null)
        {
            // 自分の場合はカメラの向きを使用
            return playerCamera.transform.rotation;
        }
        else if (magicSpawnPoint != null)
        {
            // 他プレイヤーの場合はmagicSpawnPointの向きを使用
            return magicSpawnPoint.rotation;
        }
        else
        {
            // どちらもない場合はプレイヤーの向きを使用
            return Quaternion.Euler(rotationX, transform.eulerAngles.y, 0);
        }
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            // 他プレイヤーの視点を同期
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(networkRotationX, 0, 0);
                transform.rotation = Quaternion.Euler(0, networkRotationY, 0);
            }
            return;
        }

        // 移動処理
        isGrounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, groundCheckDistance, groundLayer);

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        Vector3 move = (forward * moveInput.y * moveSpeed) + (right * moveInput.x * moveSpeed);

        if (isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = -2f;
            hasDoubleJumped = false;
        }

        moveDirection.y -= gravity * Time.deltaTime;

        characterController.Move(move * Time.deltaTime + new Vector3(0, moveDirection.y, 0) * Time.deltaTime);

        // 視点移動処理
        rotationX += -lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, lookInput.x * lookSpeed, 0);


    }

    public void DisableFiringFor(float seconds)
    {
        StartCoroutine(ReEnableFiringAfterDelay(seconds));
    }

    private IEnumerator ReEnableFiringAfterDelay(float delay)
    {
        canFire = false;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(delay);
        canFire = true;
        Debug.Log("射撃が再度有効になりました。");
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(groundCheckPoint.position, groundCheckPoint.position + Vector3.down * groundCheckDistance);
        }
    }

    // プレイヤーが破棄されるとき（シーン遷移など）にクロスヘアを非表示にする
    private void OnDestroy()
    {
        if (photonView.IsMine && crosshairImage != null)
        {
            crosshairImage.gameObject.SetActive(false);
        }
    }

    public void OnDash(InputAction.CallbackContext value)
    {
        if (!photonView.IsMine || !value.performed) return;
        if (canDash && Time.time > lastDashTime + dashCooldown)
        {
            lastDashTime = Time.time;
            StartCoroutine(PerformDash());
        }
    }

    private IEnumerator PerformDash()
    {
        float startTime = Time.time;
        Vector3 dashDirection = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
        // 入力がない場合は前方へダッシュ
        if (dashDirection.sqrMagnitude < 0.1f)
        {
            dashDirection = transform.forward;
        }

        while (Time.time < startTime + dashDuration)
        {
            characterController.Move(dashDirection * dashSpeed * Time.deltaTime);
            yield return null;
        }
    }
}