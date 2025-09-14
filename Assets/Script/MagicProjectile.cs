using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : MonoBehaviour
{
    private PhotonView photonView;
    private Rigidbody rb;

    [Header("設定")]
    public int baseDamage = 25;
    public float lifetime = 5.0f;

    private int bouncesRemaining = 0; // 残りの反射回数
    private float currentSpeed; // 現在の速度を保持

    private Transform target; //追尾相手
    private float homingStrength = 4f; // 追尾の強さ
    private float homingSearchRadius = 20f; // 索敵範囲
    private float homingActivationTime; // 追尾を開始する時間
    private float homingDelay = 0.1f;   // 追尾開始までの遅延時間（秒）

    private float maxHomingAngle = 90f; // この角度以上ターゲットが離れたら追尾をやめる

    private GameObject attacker;
    private int finalDamage;
    private bool isDestroyed = false; // 重複破壊を防ぐフラグ

    public GameObject explosionEffectPrefab;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody>();

        // 重力を無効化（弾が落下しないように）
        rb.useGravity = false;

        // 物理挙動の設定
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        // 所有者（撃ったプレイヤー）のGameObjectを特定する
        Player owner = photonView.Owner;
        if (owner != null)
        {
            foreach (var view in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
            {
                if (view.Owner == owner && view.CompareTag("Player"))
                {
                    attacker = view.gameObject;
                    break;
                }
            }
        }

        // 攻撃者が見つからない場合は弾を削除
        if (attacker == null)
        {
            if (photonView.IsMine)
            {
                SafeDestroy();
            }
            return;
        }



        // 弾の所有者（撃った本人）だけが、弾を動かし、ダメージを計算する
        if (photonView.IsMine)
        {
            NewPlayerController ownerController = attacker.GetComponent<NewPlayerController>();

            if (ownerController != null && ownerController.hasHomingProjectiles)
            {
                FindTarget();
                homingActivationTime = Time.time + homingDelay;
            }

            if (ownerController != null)
            {
                finalDamage = baseDamage + ownerController.bonusDamage;
                currentSpeed = ownerController.magicSpeed;
                rb.linearVelocity = transform.forward * currentSpeed;

                if (ownerController.projectileCanBounce)
                {
                    bouncesRemaining = 1; // 1回だけ反射できるように設定
                }
            }
            else
            {
                // コントローラーが見つからない場合はデフォルト値を使用
                finalDamage = baseDamage;
                currentSpeed = 30f;
                rb.linearVelocity = transform.forward * currentSpeed;
            }

            // lifetime秒後に消滅する処理は、所有者だけが行う
            StartCoroutine(DestroyAfterLifetime());
        }
    }

    private System.Collections.IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        SafeDestroy();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 当たり判定とダメージ処理、破壊命令は、所有者だけが行う
        if (!photonView.IsMine || isDestroyed)
        {
            return;
        }

        // 自分自身との衝突チェック
        if (collision.gameObject == attacker)
        {
            return;
        }

        NewPlayerController ownerController = attacker.GetComponent<NewPlayerController>();

        // 相手のHealthコンポーネントを探す
        Health targetHealth = collision.gameObject.GetComponent<Health>();
        if (targetHealth != null)
        {
            if (ownerController != null && ownerController.hasExplosiveShot)
            {
                // 爆発エフェクトの生成とダメージ処理を全クライアントに通知
                photonView.RPC("Rpc_Explode", RpcTarget.All, transform.position);
            }
            else
            {
                // ダメージ処理を依頼
                targetHealth.TakeDamage(finalDamage, photonView.Owner);
                SafeDestroy();
                return;
            }
        }

        // 反射回数が残っている場合
        if (bouncesRemaining > 0)
        {
            bouncesRemaining--;

            if (ownerController != null && ownerController.hasExplosiveShot)
            {
                // 爆発エフェクトの生成とダメージ処理を全クライアントに通知
                photonView.RPC("Rpc_Explode", RpcTarget.All, transform.position);
            }

            // 反射の計算
            Vector3 normal = collision.contacts[0].normal;
            Vector3 currentDirection = rb.linearVelocity.normalized;
            Vector3 reflection = Vector3.Reflect(currentDirection, normal);

            // 地面に対する反射の場合、少し上向きの角度を追加（地面に沿って滑らないように）
            if (Mathf.Abs(normal.y) > 0.7f) // 地面や天井と判定される場合
            {
                // 反射ベクトルが水平すぎる場合は、少し角度をつける
                if (Mathf.Abs(reflection.y) < 0.1f)
                {
                    reflection.y = Mathf.Sign(normal.y) * 0.3f; // 地面なら上向き、天井なら下向きに
                    reflection = reflection.normalized;
                }
            }

            if (Mathf.Abs(normal.x) > 0.7f) // 地面や天井と判定される場合
            {
                // 反射ベクトルが水平すぎる場合は、少し角度をつける
                if (Mathf.Abs(reflection.x) < 0.1f)
                {
                    reflection.x = Mathf.Sign(normal.x) * 0.3f; // 地面なら上向き、天井なら下向きに
                    reflection = reflection.normalized;
                }
            }


            // 速度を維持したまま方向だけを変更
            rb.linearVelocity = reflection * currentSpeed;

            // 弾の向きも反射方向に合わせる
            transform.rotation = Quaternion.LookRotation(reflection);

            // デバッグ用
            Debug.Log($"弾が反射しました。法線: {normal}, 新しい方向: {reflection}");
        }
        // 反射回数が残っていない場合
        else
        {
            if (ownerController != null && ownerController.hasExplosiveShot)
            {
                // 爆発エフェクトの生成とダメージ処理を全クライアントに通知
                photonView.RPC("Rpc_Explode", RpcTarget.All, transform.position);
            }
            SafeDestroy();
        }
    }

    [PunRPC]
    private void Rpc_Explode(Vector3 position)
    {
        // ここで爆発エフェクトを再生する（任意）
        Instantiate(explosionEffectPrefab, position, Quaternion.identity);

        // 範囲内のプレイヤーを探してダメージを与える（マスタークライアントのみが実行）
        if (PhotonNetwork.IsMasterClient)
        {
            Collider[] colliders = Physics.OverlapSphere(position, 3.0f /* explosionRadius */);
            foreach (var hitCollider in colliders)
            {
                Health health = hitCollider.GetComponent<Health>();
                // 攻撃者自身は爆発ダメージを受けないようにする
                if (health != null && hitCollider.gameObject != attacker)
                {
                    // 爆発ダメージは威力を少し下げるのがオススメ
                    health.TakeDamage(15 /* explosionDamage */, photonView.Owner);
                }
            }
        }
    }

    // 安全な破壊処理（重複実行を防ぐ）
    private void SafeDestroy()
    {
        if (isDestroyed || !photonView.IsMine) return;

        isDestroyed = true;

        // nullチェックを追加
        if (this != null && gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    // オブジェクトが破壊される前の処理
    private void OnDestroy()
    {
        // コルーチンが実行中の場合は停止
        StopAllCoroutines();
    }

    private void FindTarget()
    {
        float closestDistance = homingSearchRadius;
        NewPlayerController[] allPlayers = FindObjectsByType<NewPlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            // 自分自身はターゲットにしない
            if (player.gameObject == attacker) continue;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                target = player.transform;
            }
        }
    }

    // 物理挙動を一定に保つため、FixedUpdateで速度を維持
    private void FixedUpdate()
    {
        if (photonView.IsMine && rb != null && !isDestroyed)
        {
            // 速度の大きさを一定に保つ（重力や摩擦の影響を受けないように）
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * currentSpeed;
            }
        }

        if (photonView.IsMine && !isDestroyed && target != null)
        {
            if (Time.time < homingActivationTime)
            {
                // 時間が経つまでは何もしない（直進する）
                return;
            }

            Vector3 forwardDirection = rb.linearVelocity.normalized;
            Vector3 directionToTarget = (target.position - rb.position).normalized;
            float angle = Vector3.Angle(forwardDirection, directionToTarget);

            // ★★★ 角度が閾値を超えたらターゲットを外す ★★★
            if (angle > maxHomingAngle)
            {
                target = null; // ターゲットをnullにして追尾を停止
                return;        //以降の処理は行わない
            }

            Vector3 newVelocity = Vector3.RotateTowards(rb.linearVelocity.normalized, directionToTarget, homingStrength * Time.fixedDeltaTime, 0.0f);
            rb.linearVelocity = newVelocity * rb.linearVelocity.magnitude;

            // 弾の向きも進行方向に合わせる
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }


}