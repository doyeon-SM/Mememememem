/*using UnityEngine;

public class PlayerRecipeUnlockSystem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private CookSystem cookSystem;
    [SerializeField] private PlayerRecipe playerRecipe;
    [SerializeField] private PlayerInventory inventory;
    //[SerializeField] private UnLockRecipeUI unLockRecipeUI;
    [SerializeField] private RecipeData[] allRecipes;

    private void OnEnable()
    {
        if(inventory != null)
        {
            inventory.OnItemObtained += HandleItemObtained;
        }
    }
    private void Start()
    {
        allRecipes = cookSystem.GetRecipe();
    }
    private void OnDisable()
    {
        inventory.OnItemObtained -= HandleItemObtained;
    }

    private void HandleItemObtained(ItemDefinition obtainedItem, int amount)
    {
        if(cookSystem == null || playerRecipe == null ||  inventory == null) return;
        foreach(RecipeData recipe in allRecipes)
        {
            if (recipe.unlockType == RecipeUnlockType.Default || playerRecipe.RecipeIsReady(recipe)) continue;
            if(recipe.requiredUnlockItem == obtainedItem)
            {
*//*                if(playerRecipe.UnlockRecipe(recipe) && unLockRecipeUI != null)
                {
                    unLockRecipeUI.UnLockRecipe(recipe.result.icon, recipe.result.displayName);
                }*//*
            }
        }

    }
}*/
