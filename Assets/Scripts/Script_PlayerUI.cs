using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using UnityEngine;
using UnityEngine.UI;

public class Script_PlayerUI : MonoBehaviour
{
    public HTTPClient ClientScript;
    public Text Name;
    public Text Rank;
    public Text Points;

    public int ArrayPos;
    // Update is called once per frame
    void Update()
    {
        if(ClientScript.AllPlayers.Count >= ArrayPos+1)
        {
            Name.text = ClientScript.AllPlayers[ArrayPos].user_id;
            Rank.text = ClientScript.AllPlayers[ArrayPos].rank;
            Points.text = ClientScript.AllPlayers[ArrayPos].points;
        }
    }
}
