using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;

public class CorData 
{
    public EditorCoroutine coroutine1;  
    public bool cor1finished = false;
    public EditorCoroutine coroutine2;  
    public bool cor2finished = false;
    public EditorCoroutine coroutine3;  
    public bool cor3finished = false;

}   
#endif