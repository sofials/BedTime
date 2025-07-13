using UnityEngine;

public class GolemTrigger : MonoBehaviour
{  
    public GolemVillaggio golem;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            

           if (golem != null)
{
    golem.ActivateWalk();
}

            else
            {
                Debug.LogWarning("Nessun GolemVillaggio trovato nella scena.");
            }
        }
    }
}
