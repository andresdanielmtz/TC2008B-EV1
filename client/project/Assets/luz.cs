using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeLightColor : MonoBehaviour
{
    public Light targetLight; // Asigna la luz en el Inspector
    public float changeInterval = 1f; // Intervalo de tiempo en segundos para cambiar de color
    private float timer; // Temporizador interno
    private bool isRed = true; // Estado actual del color

    void Start()
    {
        // Comprueba si la luz está asignada
        if (targetLight == null)
        {
            Debug.LogError("No se ha asignado una luz. Por favor, asigna una luz en el Inspector.");
            return;
        }

        timer = changeInterval; // Inicializa el temporizador
        targetLight.color = Color.red; // Comienza con el color rojo
    }

    void Update()
    {
        timer -= Time.deltaTime; // Resta el tiempo transcurrido al temporizador

        if (timer <= 0f)
        {
            ChangeColor(); // Cambia el color de la luz
            timer = changeInterval; // Reinicia el temporizador
        }
    }

    void ChangeColor()
    {
        // Alterna entre rojo y azul
        if (isRed)
        {
            targetLight.color = Color.blue;
        }
        else
        {
            targetLight.color = Color.red;
        }

        isRed = !isRed; // Cambia el estado del color
    }
}
