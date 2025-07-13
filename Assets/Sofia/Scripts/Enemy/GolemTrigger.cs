using UnityEngine;

public class GolemTrigger : MonoBehaviour
{
    public Golem golem;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
          
        }
    }
}
