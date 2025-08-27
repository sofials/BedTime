using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [Header("Impostazioni Cursore")]
    [SerializeField] private bool hideCursorOnStart = true;
    [SerializeField] private bool lockCursor = true;
    
    void Start()
    {
        if (hideCursorOnStart)
        {
            HideCursor();
        }
    }
    
    void Update()
    {
        // Tasto ESC per mostrare/nascondere il cursore (utile per test)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }
    
    public void HideCursor()
    {
        Cursor.visible = false;
        
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    public void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    public void ToggleCursor()
    {
        if (Cursor.visible)
        {
            HideCursor();
        }
        else
        {
            ShowCursor();
        }
    }
    
    // Nascondi cursore quando la finestra ha il focus
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && hideCursorOnStart)
        {
            HideCursor();
        }
    }
    
    // Nascondi cursore quando il gioco non è in pausa
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && hideCursorOnStart)
        {
            HideCursor();
        }
    }
}