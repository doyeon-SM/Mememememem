/*using System.Collections.Generic;
using UnityEngine;

public class CookSystem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private PlayerRecipe playerRecipe;
    [Header("SetUp")]
    [SerializeField] RecipeData[] recipe;
    [SerializeField] private string[] failShowMessage;


    public int RecipeGetLength()
    {
        return recipe.Length;
    }
    public RecipeData[] GetRecipe()
    {
        return recipe;
    }
    public RecipeData GetCurrentRecipe(int index)
    {
        return recipe[index];
    }
    public bool Cook(PlayerInventory inventory, int index)
    {
        return Cooking(inventory, index);
    }
    public bool IsCook(PlayerInventory inventory, int index)
    {
        if (inventory == null || index < 0 || index >= recipe.Length || playerRecipe == null)
        {
            return false;
        }
        RecipeData _recipe = recipe[index];

        foreach (RecipeIngredient needitem in _recipe.recipe) // �����ҷ��� �������� �ʿ� ������ ���� üũ
        {
            if (inventory.GetItemAmount(needitem.item.itemId) < needitem.amount)
            {
                return false;
            }
        }
        return true;
    }
    private bool Cooking(PlayerInventory inventory, int index)
    {
        if(inventory == null || index < 0 || index >= recipe.Length || playerRecipe == null)
        {
            return false;
        }
        
        RecipeData _recipe = recipe[index];

        if(!ProssessRecipe(playerRecipe.Recipes,_recipe))
        {
            // TODO : UI������ ??? �� �����ְų� ���� ���� ��ư�� ��Ȱ��ȭ �ϴ� ������ ���� ����
            Debug.Log("�����ǰ� ���� ������ �� �����ϴ�.");
            return false;
        }
        
        foreach(RecipeIngredient needitem in _recipe.recipe) // �����ҷ��� �������� �ʿ� ������ ���� üũ
        {
            if(inventory.GetItemAmount(needitem.item.itemId) < needitem.amount)
            {
                Debug.Log("���� ������ ����");
                return false;
            }
        }

        foreach (RecipeIngredient needitem in _recipe.recipe) // �ʿ� ��� Ȯ�� �Ϸ�, ������ ���� �� �丮 ������ ����
        {
            if(!inventory.RemoveItem(needitem.item.itemId,needitem.amount))
            {
                Debug.LogWarning("���� �߻�, ���� ����Ͽ����� ����� ����");
                return false;
            }
        }
        int random = Random.Range(1, 101);
        if(random > _recipe.successChance)
        {
            //Debug.Log("���� �̲����� �丮 ������ �����߽��ϴ�...");
*//*            if(UIToastController.Instance != null && failShowMessage.Length >= 1)
            {
                UIToastController.Instance.ShowMessage(failShowMessage[Random.Range(0,failShowMessage.Length)]);
            }*//*
            return false;
        }

        // Sound
        //if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(Sfx.UICooking);

        inventory.AddItem(_recipe.result,_recipe.outputAmount);
        Debug.Log($"{_recipe.result.displayName}�� {_recipe.outputAmount}��ŭ ȹ��");
        return true;
    }

    private bool ProssessRecipe(IReadOnlyList<RecipeData> data, RecipeData needRecipe) 
    {
        foreach(RecipeData _myRecipe in data)
        {
            if (_myRecipe == needRecipe) return true;
        }
        return false;
    } 
    
}
*/