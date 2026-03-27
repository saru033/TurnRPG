/// <summary>
/// 노드 타입 정의
/// 0 : Normal   (일반 몬스터)
/// 1 : Elite    (엘리트 몬스터)
/// 2 : Rest     (휴식)
/// 3 : Event    (이벤트)
/// 4 : Shop     (상점)
/// 5 : StartHub
/// 6 : GoalHub
/// </summary>
public enum NodeType
{
    Normal   = 0,
    Elite    = 1,
    Rest     = 2,
    Event    = 3,
    Shop     = 4,
    StartHub = 5,
    GoalHub  = 6,
}