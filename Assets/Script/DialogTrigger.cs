using UnityEngine;

public class DialogTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D()
    {
        DialogManager.Instance.EnqueueMessage("이 사태를 파악하기 위해 일단 캘컴 본사쪽으로 가보자!");
        DialogManager.Instance.EnqueueMessage("이미 캘컴의 전자파로 인해 뇌가 손상되어 좀비가 된 사람도 있을거야...,");
        DialogManager.Instance.EnqueueMessage("조심해서 가보자..");
    }
}