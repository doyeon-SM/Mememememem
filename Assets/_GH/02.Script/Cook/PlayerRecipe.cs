using System.Collections.Generic;
using UnityEngine;

public class PlayerRecipe : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] CookSystem cookSystem;
    [SerializeField] List<RecipeData> myRecipe = new List<RecipeData>();
    public IReadOnlyList<RecipeData> Recipes => myRecipe;
    private void Awake()
    {
        if (cookSystem == null) return;
        myRecipe.Clear();
        foreach (RecipeData recipe in cookSystem.GetRecipe())
        {
            if(recipe.unlockType == RecipeUnlockType.Default)
            {
                myRecipe.Add(recipe);
            }
        }
    }
    public bool RecipeIsReady(RecipeData data)
    {
        return myRecipe.Contains(data);
    }
    public bool UnlockRecipe(RecipeData data)
    {
        if (data == null || myRecipe.Contains(data)) return false;
        myRecipe.Add(data);
        return true;
    }
}
