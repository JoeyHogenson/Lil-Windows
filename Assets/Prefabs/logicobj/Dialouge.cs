using UnityEngine;

public class Dialouge
{
    public string[] dialouge;
    public int index = 0;
    private string nextLine(){
        return dialouge[index];
        index++;
    }
    private void FileToDia(string filepath){
        ////reads from file to the array, making each line its own string.
    }

}
