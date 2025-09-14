using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Photon.Pun; // ★ PhotonViewを使うために追加

public class DamageEffectController : MonoBehaviour
{
    // ★ インスペクターから設定する必要がなくなるので、[SerializeField]を削除してもOK
    private Image damageImage;

    [SerializeField]
    private float fadeDuration = 0.5f;

    private Coroutine fadeCoroutine;
    private PhotonView photonView; // ★ PhotonViewへの参照を追加

    // ★★★ Awakeメソッドを追加 ★★★
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();

        // 自分が操作するキャラクターの場合のみ、UIを探して設定する
        if (photonView.IsMine)
        {
            GameObject uiObject = GameObject.FindWithTag("DamageEffectUI");
            if (uiObject != null)
            {
                damageImage = uiObject.GetComponent<Image>();
                if (damageImage == null)
                {
                    Debug.LogError("DamageEffectUIタグを持つオブジェクトにImageコンポーネントがありません！");
                }
            }
            else
            {
                Debug.LogError("シーン内に 'DamageEffectUI' タグを持つオブジェクトが見つかりません！");
            }
        }
    }


    // ダメージエフェクトを再生する
    public void PlayEffect()
    {
        // damageImageが設定されていない（自分のキャラクターではない）場合は何もしない
        if (damageImage == null) return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        Color tempColor = damageImage.color;
        tempColor.a = 0.4f;
        damageImage.color = tempColor;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            tempColor.a = Mathf.Lerp(0.4f, 0f, timer / fadeDuration);
            damageImage.color = tempColor;
            yield return null;
        }

        tempColor.a = 0f;
        damageImage.color = tempColor;
    }
}