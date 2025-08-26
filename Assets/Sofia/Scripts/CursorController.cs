using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Header("Cursor Settings")]
    [SerializeField] private bool hideCursorOnStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private bool allowToggle = true;
    
    private bool cursorHidden = false;
    
    void Start()
    {
        if (hideCursorOnStart)
        {
            HideCursor();
        }
    }
    
    void Update()
    {
        if (allowToggle && Input.GetKeyDown(toggleKey))
        {
            ToggleCursor();
        }
    }
    
    /// <summary>
    /// Nasconde il cursore ma lo lascia libero di muoversi
    /// </summary>
    public void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None; // Libero di muoversi!
        cursorHidden = true;
        
        Debug.Log("Cursore nascosto (ma può muoversi)");
    }
    
    /// <summary>
    /// Mostra il cursore
    /// </summary>
    public void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        cursorHidden = false;
        
        Debug.Log("Cursore mostrato");
    }
    
    /// <summary>
    /// Alterna tra nascosto e visibile
    /// </summary>
    public void ToggleCursor()
    {
        if (cursorHidden)
        {
            ShowCursor();
        }
        else
        {
            HideCursor();
        }
    }
    
    /// <summary>
    /// Controlla se il cursore è attualmente nascosto
    /// </summary>
    public bool IsCursorHidden()
    {
        return cursorHidden;
    }
    
    // Metodi di utilità per chiamate da altri script
    public void SetCursorVisibility(bool visible)
    {
        if (visible)
            ShowCursor();
        else
            HideCursor();
    }
}