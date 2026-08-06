public interface IRecord
{
    // TerritoryRecord 파일이 존재하지 않아 파일을 만들 때, 각 DataRecord에서 기본 JSON구조를 파일에 추가
    void InitDefaultData(ref SaveData saveData);

    // 이벤트 구독을 통한 각 DataRecord 모듈을 TerritoryRecord 파일에 오버라이트 진행
    void SaveData(string saveFilePath);

    // 게임 재시작, 씬 이동시 RecordManager의 호출을 감지하여 TerritoryRecord파일을 읽어오기
    // 이후, 각 DataRecord 모듈에서 다루는 Key값을 읽어서 각 오브젝트에 데이터 연결처리
    void ApplyData(SaveData saveData, SceneType sceneType);
}