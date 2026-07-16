using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;

    [Header("UI Elements")]
    public GameObject dialogBox;
    public TextMeshProUGUI dialogText;
    public GameObject continueIcon;

    private Queue<string> messageQueue = new Queue<string>();
    public bool isDisplaying = false;
    private bool waitingForInput = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        dialogBox.SetActive(false);
        continueIcon.SetActive(false);
    }

    private void Update()
    {
        if (waitingForInput && Input.GetKeyDown(KeyCode.Return))
        {
            waitingForInput = false;
        }
    }

    public void EnqueueMessage(string message)
    {
        messageQueue.Enqueue(message);

        if (!isDisplaying)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isDisplaying = true;
        dialogBox.SetActive(true);

        while (messageQueue.Count > 0)
        {
            string msg = messageQueue.Dequeue();
            dialogText.text = "";

            // 한 글자씩 출력
            for (int i = 0; i < msg.Length; i++)
            {
                dialogText.text += msg[i];
                yield return new WaitForSeconds(0.05f); // 글자 나오는 속도 조절
            }

            waitingForInput = true;
            continueIcon.SetActive(true);

            yield return new WaitUntil(() => !waitingForInput);

            continueIcon.SetActive(false);
        }

        dialogBox.SetActive(false);
        isDisplaying = false;
    }
}