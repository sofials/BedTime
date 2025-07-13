using UnityEngine;

public class Presents : MonoBehaviour
{
    [HideInInspector] public GameManager gameManager;

    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!collected && other.CompareTag("Player"))
        {
            collected = true;
            gameManager?.NotifyCollected(this);

            GetComponent<Collider>().enabled = false;
            gameObject.SetActive(false);
        }
    }
}
