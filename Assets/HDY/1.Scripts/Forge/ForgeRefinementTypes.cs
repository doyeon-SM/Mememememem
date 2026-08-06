namespace HDY.Forge
{
    /// <summary>연마 시도가 거부됐을 때의 사유.</summary>
    public enum RefinementFailReason
    {
        None,
        NotForgeableTool,
        NoInstanceData,
        NotEnoughMaterial,
        NotEnoughGold,
        MissingDependency
    }

    /// <summary>연마 실행 버튼을 누르기 전 미리 보여줄 비용 정보. 실행하지 않고 조회만 한다.</summary>
    public readonly struct RefinementPreview
    {
        public readonly RefinementFailReason BlockReason;
        public readonly string MaterialItemId;
        public readonly int MaterialCost;
        public readonly int MaterialOwned;
        public readonly int GoldCost;
        public readonly int GoldOwned;

        public RefinementPreview(RefinementFailReason blockReason, string materialItemId, int materialCost,
            int materialOwned, int goldCost, int goldOwned)
        {
            BlockReason = blockReason;
            MaterialItemId = materialItemId;
            MaterialCost = materialCost;
            MaterialOwned = materialOwned;
            GoldCost = goldCost;
            GoldOwned = goldOwned;
        }

        public bool HasEnoughToAttempt => BlockReason == RefinementFailReason.None
            && MaterialOwned >= MaterialCost && GoldOwned >= GoldCost;

        public static RefinementPreview Blocked(RefinementFailReason reason)
        {
            return new RefinementPreview(reason, null, 0, 0, 0, 0);
        }
    }

    /// <summary>연마 시도 한 번의 결과. 실패라는 결과 자체는 없다 - 잠기지 않은 칸은 항상 재판정된다.</summary>
    public readonly struct RefinementOutcome
    {
        public readonly bool Attempted;
        public readonly RefinementFailReason FailReason;
        public readonly ForgeRefinementSlotData[] UpdatedSlots;

        public RefinementOutcome(bool attempted, RefinementFailReason failReason, ForgeRefinementSlotData[] updatedSlots)
        {
            Attempted = attempted;
            FailReason = failReason;
            UpdatedSlots = updatedSlots;
        }

        public static RefinementOutcome Rejected(RefinementFailReason reason)
        {
            return new RefinementOutcome(false, reason, null);
        }
    }

    /// <summary>전승 시도가 거부됐을 때의 사유.</summary>
    public enum InheritanceFailReason
    {
        None,
        NotForgeableTool,
        ToolTypeMismatch,
        SameStack,
        MissingDependency
    }

    /// <summary>전승 시도 결과. 조건만 맞으면 무조건 성공한다(확률 없음).</summary>
    public readonly struct InheritanceOutcome
    {
        public readonly bool Attempted;
        public readonly InheritanceFailReason FailReason;

        public InheritanceOutcome(bool attempted, InheritanceFailReason failReason)
        {
            Attempted = attempted;
            FailReason = failReason;
        }

        public static InheritanceOutcome Rejected(InheritanceFailReason reason)
        {
            return new InheritanceOutcome(false, reason);
        }

        public static readonly InheritanceOutcome Success = new InheritanceOutcome(true, InheritanceFailReason.None);
    }
}
