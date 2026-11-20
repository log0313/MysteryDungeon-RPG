flowchart TD
    subgraph Client [Unity Client]
        Master[(MasterData\nScriptableObject)]
        Account[(AccountData\n영구 세션 캐시)]
        Bag[(DungeonBag\n휘발성 임시 가방)]
        
        Town[마을 씬\n상점 / 창고]
        Dungeon[던전 씬\n탐험 / 전투 / 루팅]
        
        Master -. 스펙 참조 .-> Town
        Master -. 스펙 참조 .-> Dungeon
        
        Town <-->|구매 / 판매 / 보관| Account
        Town -->|던전 진입: 가방 초기화| Bag
        Dungeon -->|전리품 획득| Bag
        
        Bag -->|전멸 시: 파기 Clear| Town
        Bag -->|탈출 성공: 정산 Commit| Account
    end

    subgraph Backend [Backend & DB]
        DB[(Cloud DB\nUsers / Storage / Progress)]
    end

    Account <==>|트랜잭션 동기화\nJSON / HTTPS| DB
