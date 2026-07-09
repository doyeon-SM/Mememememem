/*using System;
using UnityEngine;
[Serializable]
public struct RecipeIngredient
{
    public ItemDefinition item;
    public int amount;
}
public enum RecipeUnlockType
{
    Default,
    ItemObtained
}
[CreateAssetMenu(fileName = "Recipe_", menuName = "Scriptable Objects/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("기본 설정")]
    public string recipeId; 
    public ItemDefinition result; // 요리가 완성될 경우 플레이어가 가질 아이템
    public int outputAmount; // 드롭 수량
    public float successChance; // 요리 성공 확률 -> 멘탈 영향이 있기에 확률 필요

    [Header("조합 재료")]
    public RecipeIngredient[] recipe; // string, int 로 구성된 구조체

    [Header("레시피 해금 시스템")] // 분량 보고 삭제 가능성 존재함
    public RecipeUnlockType unlockType;
    public ItemDefinition requiredUnlockItem; // 만약 아이템을 습득 할 때 레시피가 해금되는 경우에만 설정
}*/