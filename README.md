# 🗡️ Mystery Dungeon-style Roguelike RPG (Unity C#)

> **포켓몬 불가사의 던전 스타일의 로그라이크 RPG 코어 시스템 프로토타입**  
> 정적/동적 데이터 분리 아키텍처와 출정-전멸-탈출로 이어지는 트랜잭션 기반 인벤토리 무결성 설계에 중점을 둔 프로젝트입니다.

---

## 🛠️ Tech Stack & Architecture Highlights
* **Engine & Language:** Unity (C#), ScriptableObject
* **Core Patterns:** Singleton (`DontDestroyOnLoad`), SRP(단일 책임 원칙) 기반 계층 분리, Fisher-Yates 셔플 알고리즘
* **Data & Persistence:** JSON Serialization (DTO), Cloud DB 트랜잭션 동기화 예정

---

## 1. 게임 데이터 관리 구조

게임 데이터는 변하지 않는 기획 스펙과 유저별 런타임 상태의 생명주기를 분리하기 위해 **`MasterData`**, **`AccountData`**, **`DungeonBag`**, **`EquippedItems`** 4가지 핵심 계층으로 설계했습니다.

| 데이터 계층 | 역할 | 생명주기 / 유지 방식 |
| :--- | :--- | :--- |
| **`MasterData`** | 아이템 및 장비의 정적 게임 스펙 (`ScriptableObject`) | 고정 (클라이언트 내장) |
| **`AccountData`** | 플레이어 영구 계정 데이터, 마을 재화, 창고 인벤토리 | 영구 유지 (Cloud DB 동기화) |
| **`DungeonBag`** | 던전 출정 소지품(지참 골드/아이템) 및 탐험 획득 전리품 | 출정 및 정산 단위 관리 |
| **`EquippedItems`** | 현재 장착 중인 무기, 방어구, 액세서리 | 마을과 던전 간 상시 유지 |

### 1-1. `MasterData`
* `ScriptableObject` 기반 정적 게임 데이터로 메모리 중복 할당 방지
* 아이템 및 장비의 기본 스펙(가격, 최대 중첩 수, 설명 등) 관리
* 아이템 타입(`ItemType`: Equipment, Consumable, Material) 및 장비 부위 정보 제공
* 마을과 던전 씬에서 공통으로 참조

### 1-2. `AccountData`
* 플레이어의 영구 데이터 관리 (`[System.Serializable]` 직렬화 구조)
* 마을 보유 골드(`gold`) 관리
* 마을 창고 영구 아이템(`storageItems`) 및 던전 클리어 이력 관리
* 추후 백엔드 Cloud DB와 JSON 패킷 단위로 데이터 동기화

### 1-3. `DungeonBag`
* 던전 출정 시 지참하는 아이템(`bagItems`)과 출정 골드 관리
* 마을 상점에서 구매하거나 창고에서 꺼낸 출정 준비물 보관
* 던전 탐험 중 전투 및 루팅으로 획득한 전리품·골드 실시간 누적
* **전멸 시:** 장착 장비를 제외한 비장착 소지품에 부분 손실 페널티 적용
* **탈출 성공 시:** 영구 데이터(`AccountData`)로 일괄 정산(Commit)

### 1-4. `EquippedItems`
* 현재 플레이어가 장착 중인 무기, 방어구, 액세서리를 `List<InventorySlot>` 형태로 별도 관리
* 던전 진입 및 마을 복귀 시 장착 상태 100% 유지
* **전멸 페널티 대상에서 완전 제외**되어 전멸 시에도 장착 장비는 손실되지 않음

---

## 2. 마을 및 출정 준비 시스템

던전 출정 전 마을에서 상점 거래, 창고 정리, 장비 장착을 통해 탐험을 준비합니다.

### 2-1. 상점 (Shop)
* 플레이어가 보유 골드를 사용하여 아이템 및 장비 구매
* 구매한 아이템은 설정에 따라 출정 가방(`DungeonBag`) 또는 창고(`AccountData`)에 보관
* 불필요한 아이템 판매를 통한 골드 획득 및 `AccountData` 즉시 반영

### 2-2. 창고 및 가방 아이템 이동
마을 창고(`storageItems`)와 출정 가방(`bagItems`) 사이에 아이템을 자유롭게 이동할 수 있습니다.

| 메서드 | 기능 |
| :--- | :--- |
| `MoveItemToBag(int storageSlotIndex, int quantity)` | 마을 창고에서 출정 가방으로 지정 수량만큼 아이템 이동 |
| `MoveItemToStorage(int bagSlotIndex, int quantity)` | 출정 가방에서 마을 창고로 지정 수량만큼 아이템 이동 |

* 중첩 가능한 소비/재료 아이템의 경우 수량 단위 부분 이동 지원
* 마을 보유 골드와 던전 출정 지참 골드를 분리하여 리스크 관리 가능

### 2-3. 장비 장착 및 해제

| 메서드 | 기능 |
| :--- | :--- |
| `EquipItemFromBag(int bagSlotIndex)` | 가방의 장비 아이템을 장착 슬롯(`EquippedItems`)으로 이동 |
| `UnequipItemToBag(int equipSlotIndex)` | 장착 중인 장비를 해제하여 일반 가방(`DungeonBag`)으로 반환 |

* `ItemType.Equipment` 타입의 아이템만 장착 허용
* 동일한 장비 부위에 기존 장비가 장착되어 있을 경우, 기존 장비를 가방으로 자동 반환 후 교체
* 장착 장비와 일반 가방 아이템을 분리 관리하여 전멸 페널티 예외 처리 보장

---

## 3. 던전 출정 및 탐험 시스템

### 3-1. 던전 진입 (`EnterDungeon`)
* 출정 준비 단계에서 구성한 `DungeonBag`을 초기화하지 않고 그대로 유지한 채 던전에 진입
* 지참 골드 및 `EquippedItems`의 장착 상태 역시 동일하게 유지

### 3-2. 던전 탐험 및 루팅 (`AddLootInDungeon`)
* 던전 내 전투, 보물상자 등을 통해 전리품 및 골드 획득
* 획득한 아이템은 `DungeonBag.bagItems`에 추가(중첩 가능 아이템은 자동 병합)
* 획득한 골드는 `DungeonBag`의 지참 골드에 누적

---

## 4. 던전 전멸 페널티 (`OnPlayerDefeated`)

던전 탐험 중 전멸 시 가방 전체를 무조건 초기화하지 않고, **장착 장비는 보호하면서 비장착 소지품과 지참 골드에만 부분 손실 페널티**를 적용합니다.

### 4-1. 골드 페널티
* 현재 `DungeonBag`에 보유한 지참 골드의 **10% 차감** (소수점 이하 버림, 나머지 90% 유지)
* *예시:* `1,000 골드` 지참 시 → `100 골드` 손실 → `900 골드` 유지

### 4-2. 비장착 아이템 부분 손실 페널티 (Fisher-Yates 셔플)
* 일반 가방(`bagItems`)에 들어있는 비장착 아이템만 손실 대상으로 지정
* 정확한 확률 분배를 위해 슬롯 내 중첩된 아이템을 **개별 수량 단위 리스트로 전개**
* **Fisher-Yates 알고리즘**으로 무작위 셔플 후 전체 개별 수량의 **50%(소수점 버림)를 제거**
* 남은 아이템을 다시 가방 슬롯 형태로 재구성
* *예시:* 총 6개(오랭열매 4개, 탈출구슬 2개) 소지 시 → 개별 전개 후 무작위 3개 손실 → 3개 유지

### 4-3. 장착 장비 100% 보호
* `EquippedItems`에 등록된 무기, 방어구, 액세서리는 전멸 페널티 대상에서 완전 제외
* 전멸 후 마을 복귀 시에도 강화 수치 및 장착 상태 그대로 보존

### 4-4. 전멸 디버그 로그 출력
전멸 판정 시 Unity Console에 손실 내역과 장비 보호 여부를 명확히 출력합니다.
```text
[Defeat Penalty] 전멸 페널티 발생: 골드 100 소실, 잃어버린 가방 아이템 목록: [오랭열매 x2, 회복 물약 x1]
[Defeat Penalty] 장착 장비는 안전하게 보존됨
