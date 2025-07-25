#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#pragma comment(lib, "Ws2_32.lib")
#include <mysql/mysql.h>
#include <iostream>
#include <cstring>
#include <thread>
#include <nlohmann/json.hpp>
#include <curl/curl.h>

using namespace std;
using json = nlohmann::json;

const char* HOST = "127.0.0.1";
const char* USER = "iot";
const char* PASS = "1234";
const char* DB = "MAC";

constexpr int PORT = 9034;

static size_t WriteCallback(void* contents, size_t size, size_t nmemb, void* userp) {
    ((string*)userp)->append((char*)contents, size * nmemb);
    return size * nmemb;
}

json classify_via_http(const string& text,
    const string& host = "127.0.0.1",
    int port = 8000)
{
    cout << "[DBG] calling /classify\n";
    CURL* curl = curl_easy_init();
    string url = "http://" + host + ":" + to_string(port) + "/classify";
    string readBuf, body = json{ {"text", text} }.dump();
    struct curl_slist* hdrs = nullptr;
    hdrs = curl_slist_append(hdrs, "Content-Type: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, hdrs);
    curl_easy_setopt(curl, CURLOPT_POST, 1L);
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, body.c_str());
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &readBuf);

    CURLcode res = curl_easy_perform(curl);
    curl_slist_free_all(hdrs);
    curl_easy_cleanup(curl);
    if (res != CURLE_OK)
        throw runtime_error(string("curl failed: ") + curl_easy_strerror(res));
    cout << "[DBG] /classify response: " << readBuf << "\n";
    return json::parse(readBuf);
}

json parse_order_via_http(const string& text,
    const string& host = "127.0.0.1",
    int port = 8000)
{
    cout << "[DBG] calling /parse_order\n";
    CURL* curl = curl_easy_init();
    string url = "http://" + host + ":" + to_string(port) + "/parse_order";
    string readBuf, body = json{ {"text", text} }.dump();

    struct curl_slist* hdrs = nullptr;
    hdrs = curl_slist_append(hdrs, "Content-Type: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, hdrs);
    curl_easy_setopt(curl, CURLOPT_POST, 1L);
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, body.c_str());
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &readBuf);

    CURLcode res = curl_easy_perform(curl);
    curl_slist_free_all(hdrs);
    curl_easy_cleanup(curl);
    if (res != CURLE_OK)
        throw runtime_error(string("curl failed: ") + curl_easy_strerror(res));

    cout << "[DBG] /parse_order response: " << readBuf << "\n";
    return json::parse(readBuf);
}

// DB 연결
MYSQL* connect_db() {
    MYSQL* conn = mysql_init(nullptr);
    if (!mysql_real_connect(conn, HOST, USER, PASS, DB, 0, NULL, 0)) {
        cerr << "DB 연결 실패: " << mysql_error(conn) << endl;
        return nullptr;
    }

    //  UTF-8 문자셋 설정 (이모지 포함 지원)
    if (mysql_set_character_set(conn, "utf8mb4")) {
        cerr << "문자셋 설정 실패: " << mysql_error(conn) << endl;
        WSACleanup();
        mysql_close(conn);
        return nullptr;
    }

    cout << "DB 연결 성공 (문자셋: " << mysql_character_set_name(conn) << ")" << endl;
    return conn;
}

json order_menu(const json& data) {
    json result;
    MYSQL* conn = connect_db();
    if (!conn) {
        result["status"] = "fail";
        result["message"] = "DB연결실패";
        return result;
    }

    // 1) 입력값 추출
    string M_NAME = data.get<string>();

    // 2) 쿼리 준비 (공백·탭 제거 비교)
    const char* sql =
        "SELECT M_NAME, M_PRICE "
        "FROM MENU_INFO "
        "WHERE REPLACE(REPLACE(TRIM(M_NAME), ' ', ''), '\t', '') "
        "  = REPLACE(REPLACE(TRIM(?), ' ', ''), '\t', '')";

    MYSQL_STMT* stmt = mysql_stmt_init(conn);
    if (!stmt || mysql_stmt_prepare(stmt, sql, strlen(sql)) != 0) {
        result["status"] = "error";
        result["message"] = stmt
            ? mysql_stmt_error(stmt)
            : mysql_error(conn);
        mysql_stmt_close(stmt);
        mysql_close(conn);
        return result;
    }

    // 3) 파라미터 바인딩
    MYSQL_BIND bind_param;
    memset(&bind_param, 0, sizeof(bind_param));
    bind_param.buffer_type = MYSQL_TYPE_STRING;
    bind_param.buffer = (char*)M_NAME.c_str();
    bind_param.buffer_length = (unsigned long)M_NAME.size();
    if (mysql_stmt_bind_param(stmt, &bind_param) != 0) {
        result["status"] = "error";
        result["message"] = mysql_stmt_error(stmt);
        mysql_stmt_close(stmt);
        mysql_close(conn);
        return result;
    }

    // 4) 쿼리 실행
    if (mysql_stmt_execute(stmt) != 0) {
        result["status"] = "error";
        result["message"] = mysql_stmt_error(stmt);
        mysql_stmt_close(stmt);
        mysql_close(conn);
        return result;
    }

    // 5) 결과 바인딩
    MYSQL_BIND bind_result[2];
    memset(bind_result, 0, sizeof(bind_result));
    char  name_buf[256]; unsigned long name_len;
    char  price_buf[64]; unsigned long price_len;
    bind_result[0].buffer_type = MYSQL_TYPE_STRING;
    bind_result[0].buffer = name_buf;
    bind_result[0].buffer_length = sizeof(name_buf);
    bind_result[0].length = &name_len;
    bind_result[1].buffer_type = MYSQL_TYPE_STRING;
    bind_result[1].buffer = price_buf;
    bind_result[1].buffer_length = sizeof(price_buf);
    bind_result[1].length = &price_len;
    mysql_stmt_bind_result(stmt, bind_result);

    // 6) Fetch
    if (mysql_stmt_fetch(stmt) == 0) {
        result["status"] = "success";
        result["message"] = "성공";
        result["M_NAME"] = string(name_buf, name_len);
        result["M_PRICE"] = string(price_buf, price_len);
    }
    else {
        result["status"] = "fail";
        result["message"] = "메뉴없음";
    }

    // 7) 정리
    mysql_stmt_close(stmt);
    mysql_close(conn);
    return result;
}

// ===== get_menu 함수 =====
json get_menu(const json& data, bool is_set) {
    // 1) DB 연결
    MYSQL* conn = connect_db();
    if (!conn) {
        return { {"status","fail"},{"message","DB 연결 실패"} };
    }

    bool allOk = true;
    double setPrice = 0;
    json   setOptions = json::array();

    // 2) 세트 주문이면 단 한 번만 SET_PRICE와 옵션 목록 조회
    if (is_set) {
        // 2-1) SET_PRICE 조회
        const char* price_sql =
            "SELECT SET_PRICE "
            "FROM MENU_INFO "
            "WHERE REPLACE(REPLACE(TRIM(M_NAME),' ',''),'\t','') = "
            "      REPLACE(REPLACE(TRIM(?),' ',''),'\t','')";
        MYSQL_STMT* stmt0 = mysql_stmt_init(conn);
        mysql_stmt_prepare(stmt0, price_sql, strlen(price_sql));

        const auto& orders = data["items"]["orders"];
        if (orders.empty()) {
            return {
                {"status", "fail"},
                {"message", "주문 아이템이 없습니다"}
            };
        }

        // 첫 번째 main 메뉴 이름 가져오기
        string mainName = data["items"]["orders"][0]["name"].get<string>();

        MYSQL_BIND bp0{};
        bp0.buffer_type = MYSQL_TYPE_STRING;
        bp0.buffer = (char*)mainName.c_str();
        bp0.buffer_length = mainName.size();
        mysql_stmt_bind_param(stmt0, &bp0);

        MYSQL_BIND br0{};
        br0.buffer_type = MYSQL_TYPE_DOUBLE;
        br0.buffer = &setPrice;
        br0.buffer_length = sizeof(setPrice);
        mysql_stmt_bind_result(stmt0, &br0);

        mysql_stmt_execute(stmt0);
        mysql_stmt_fetch(stmt0);
        mysql_stmt_close(stmt0);

        // 2-2) 세트 옵션 목록 조회 (ID 제거)
        const char* opt_sql =
            "SELECT B_SET_OP_NAME, B_SET_CAT, B_SET_OP_PRICE, B_SET_LOP_PRICE "
            "FROM SET_OPT ";
        MYSQL_STMT* stmt1 = mysql_stmt_init(conn);
        mysql_stmt_prepare(stmt1, opt_sql, strlen(opt_sql));

        MYSQL_BIND bp1{};
        bp1.buffer_type = MYSQL_TYPE_STRING;
        bp1.buffer = (char*)mainName.c_str();
        bp1.buffer_length = mainName.size();
        mysql_stmt_bind_param(stmt1, &bp1);

        mysql_stmt_execute(stmt1);
        mysql_stmt_store_result(stmt1);

        // 옵션 컬럼 바인딩
        char    opn[128], cat[64];
        double  op_price = 0, lop_price = 0;
        unsigned long ln_opn, ln_cat;
        MYSQL_BIND br1[4];
        memset(br1, 0, sizeof(br1));
        // 옵션명
        br1[0].buffer_type = MYSQL_TYPE_STRING;
        br1[0].buffer = opn;
        br1[0].buffer_length = sizeof(opn);
        br1[0].length = &ln_opn;
        // 카테고리
        br1[1].buffer_type = MYSQL_TYPE_STRING;
        br1[1].buffer = cat;
        br1[1].buffer_length = sizeof(cat);
        br1[1].length = &ln_cat;
        // 옵션 추가 가격
        br1[2].buffer_type = MYSQL_TYPE_DOUBLE;
        br1[2].buffer = &op_price;
        br1[2].buffer_length = sizeof(op_price);
        // 옵션 대체 가격
        br1[3].buffer_type = MYSQL_TYPE_DOUBLE;
        br1[3].buffer = &lop_price;
        br1[3].buffer_length = sizeof(lop_price);

        mysql_stmt_bind_result(stmt1, br1);

        // fetch all options
        while (mysql_stmt_fetch(stmt1) == 0) {
            setOptions.push_back({
                {"B_SET_OP_NAME",    string(opn, ln_opn)},
                {"B_SET_CAT",        string(cat, ln_cat)},
                {"B_SET_OP_PRICE",    op_price},
                {"B_SET_LOP_PRICE",   lop_price}
                });
        }
        mysql_stmt_close(stmt1);
    }

    // 3) 모든 orders 아이템에 대해 가격 결정
    json out_orders = json::array();
    for (const auto& it : data["items"]["orders"]) {
        string name = it.value("name", "");
        int    qty = it.value("qty", 1);
        string type = it.value("type", "");

        json rec;
        rec["name"] = name;
        rec["qty"] = qty;
        rec["type"] = type;

        if (type == "main") {
            // 단품 가격 조회
            const char* sql =
                "SELECT M_PRICE "
                "FROM MENU_INFO "
                "WHERE REPLACE(REPLACE(TRIM(M_NAME),' ',''),'\t','') = "
                "      REPLACE(REPLACE(TRIM(?),' ',''),'\t','')";
            MYSQL_STMT* stmt = mysql_stmt_init(conn);
            mysql_stmt_prepare(stmt, sql, strlen(sql));

            MYSQL_BIND bp{};
            bp.buffer_type = MYSQL_TYPE_STRING;
            bp.buffer = (char*)name.c_str();
            bp.buffer_length = name.size();
            mysql_stmt_bind_param(stmt, &bp);

            double menuPrice = 0;
            MYSQL_BIND br{};
            br.buffer_type = MYSQL_TYPE_DOUBLE;
            br.buffer = &menuPrice;
            br.buffer_length = sizeof(menuPrice);
            mysql_stmt_bind_result(stmt, &br);

            mysql_stmt_execute(stmt);
            if (mysql_stmt_fetch(stmt) == 0) {
                // 세트일 땐 setPrice, 아니면 menuPrice
                rec["price"] = is_set ? setPrice : menuPrice;
            }
            else {
                rec["message"] = "메뉴 정보 없음";
            }
            mysql_stmt_close(stmt);
        }
        else if (type == "side") {
            // SIDE_INFO 조회 로직 (기존과 동일)
            const char* sql =
                "SELECT SIDE_PRICE "
                "FROM SIDE_INFO "
                "WHERE REPLACE(REPLACE(TRIM(SIDE_NAME),' ',''),'\t','') = "
                "      REPLACE(REPLACE(TRIM(?),' ',''),'\t','')";
            MYSQL_STMT* stmt = mysql_stmt_init(conn);
            mysql_stmt_prepare(stmt, sql, strlen(sql));

            MYSQL_BIND bp{};
            bp.buffer_type = MYSQL_TYPE_STRING;
            bp.buffer = (char*)name.c_str();
            bp.buffer_length = name.size();
            mysql_stmt_bind_param(stmt, &bp);

            double sidePrice = 0;
            MYSQL_BIND br{};
            br.buffer_type = MYSQL_TYPE_DOUBLE;
            br.buffer = &sidePrice;
            br.buffer_length = sizeof(sidePrice);
            mysql_stmt_bind_result(stmt, &br);

            mysql_stmt_execute(stmt);
            if (mysql_stmt_fetch(stmt) == 0) {
                rec["price"] = sidePrice;
            }
            else {
                rec["message"] = "사이드 정보 없음";
            }
            mysql_stmt_close(stmt);
        }
        else {
            rec["message"] = "알 수 없는 타입";
        }

        out_orders.push_back(rec);
    }

    // 4) 커넥션 닫기
    mysql_close(conn);

    // 5) 최종 JSON 조립
    json response;
    response["status"] = "success";
    response["items"] = {
        {"orders",  out_orders},
        {"request", data["items"].value("request","")}
    };
    if (is_set) {
        // price 필드에 이미 setPrice가 들어갔으므로 별도 필드는 제거
        response["set_options"] = move(setOptions);
    }
    return response;
}

json insert_history(const json& hist) {
    json result;
    MYSQL* conn = connect_db();
    if (!conn) {
        result["status"] = "fail";
        result["message"] = "DB 연결실패";
        return result;
    }

    // 1) ORDER_INFO 삽입
    {   
        string orderNum = hist["Order_num"].get<string>();
        string orderDate = hist["Date"].get<string>();
        int totalPrice = hist["Total_Price"].get<int>();

        string sql1 =
            "INSERT INTO ORDER_INFO "
            "(ORDER_NUM, ORDER_DATE, TOTAL_PRICE) VALUES ("
            "'" + orderNum + "', "
            "'" + orderDate + "', "
            + to_string(totalPrice) +
            ")";


        if (mysql_query(conn, sql1.c_str()) != 0) {
            string err = mysql_error(conn);
            mysql_close(conn);
            cerr << "sq1:" << err << endl;
            result["status"] = "fail";
            result["message"] = mysql_error(conn);
        
            return result;
        }
    }

    long orderInfoId = mysql_insert_id(conn);

    // 2) ORDER_MENU_INFO 삽입
    {
        for (const auto& item : hist["Items"]) {
            string menu = item["Menu"].get<string>();
            int    cnt = item["Count"].get<int>();
            int price = item["Price"].get<int>();
            string req = item["Request"].is_null()
                ? ""
                : item["Request"].get<string>();

            string sql2 =
                "INSERT INTO ORDER_MENU_INFO "
                "(ORDER_INFO_ID, Menu, QTY, M_PRICE, REQUEST) VALUES ("
                + to_string(orderInfoId) + ", "
                + "'" + menu + "', "
                + to_string(cnt) + ", "
                + to_string(price) + ", "
                + "'" + req + "'" +
                ")";
            if (mysql_query(conn, sql2.c_str()) != 0) {
                string err = mysql_error(conn);
                mysql_close(conn);
                cerr << "sql2:" << err << endl;
                result["status"] = "fail";
                result["message"] = mysql_error(conn);
                return result;
            }

            long orderMenuId = mysql_insert_id(conn);

            // 3) ORDER_OPT_INFO 삽입
            for (const auto& v : item["Set_Id"]) {
                int setId = v.get<int>();
                if (setId <= 0) {
                    continue;
                }
                string sql3 =
                    "INSERT INTO ORDER_OPT_INFO "
                    "(ORDER_M_ID, SET_ID) VALUES ("
                    + to_string(orderMenuId) + ", "
                    + to_string(setId) +
                    ")";

                if (mysql_query(conn, sql3.c_str()) != 0) {
                    string err = mysql_error(conn);
                    mysql_close(conn);
                    cerr << "sql3" << err << endl;
                    result["status"] = "fail";
                    result["message"] = mysql_error(conn);
                    return result;
                }

            }

        }
    }
    mysql_close(conn);
    result["status"] = "success";
    result["message"] = "축하합니다";
    return result;
}


// 간단 디버그용 클라이언트 처리 함수
void handleClient(SOCKET clientSocket) {
    while (true) {

        char buffer[4096];
        int len = recv(clientSocket, buffer, sizeof(buffer) - 1, 0);
        if (len <= 0) {
            cout << "클라이언트 연결 종료됨." << endl;
            break;
        }

        string received_data(buffer, len);
        cout << "받은 데이터: " << received_data << endl;

        json request = json::parse(received_data);

        string action = request["action"];
        json response;
      
        if (action == "order") {
            bool is_set = request["is_set"];
            json data = request["data"];

            string text = request["data"].get<string>();
            cout << text << endl;
         
            auto cls = classify_via_http(text);
            cout << "[DBG] classification JSON: " << cls.dump() << "\n";
            string label = cls.value("label", "");

            if (label == "menu") {
          
                json parsed = parse_order_via_http(text);
                cout << parsed.dump(2) << endl;
                
                //response = get_menu(parsed);
                response = get_menu(parsed,is_set);
                //response = order_menu(data);
            } else {
                response = {
                    {"status" , "fail"},
                    {"message" , "주문아님"}
                };
            }

            //response = order_menu(data);
        }
        else if (action == "payment") {
            // payment 분기에서는 “history” 키를 꺼내야 함
            json history = request.value("history", json::object());
            cout << "결제 요청 데이터: " << history.dump() << endl;
            response = insert_history(history);
        }

        string reply = response.dump() + "\n";

        // JSON 전송
        int sent = send(clientSocket, reply.c_str(), static_cast<int>(reply.size()), 0);

        if (sent == SOCKET_ERROR) {
            cerr << "send() failed: " << WSAGetLastError() << endl;
        }
        else {
            cout << "Sent JSON: " << reply << endl;
        }
    }
    closesocket(clientSocket);
}

int main() {
    SetConsoleCP(CP_UTF8);
    SetConsoleOutputCP(CP_UTF8);
    // Winsock 초기화
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        cerr << "WSAStartup failed: " << WSAGetLastError() << endl;
        return 1;
    }
    // 서버 소켓 생성 및 바인딩
    SOCKET serverSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (serverSocket == INVALID_SOCKET) {
        cerr << "socket failed: " << WSAGetLastError() << endl;
        WSACleanup();
        return 1;
    }

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(PORT);

    if (::bind(serverSocket, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) == SOCKET_ERROR) {
        cerr << "bind failed: " << WSAGetLastError() << endl;
        closesocket(serverSocket);
        WSACleanup();
        return 1;
    }
    if (listen(serverSocket, 5) == SOCKET_ERROR) {
        cerr << "listen failed: " << WSAGetLastError() << endl;
        closesocket(serverSocket);
        WSACleanup();
        return 1;
    }
    cout << "Server listening on port "<< PORT << endl;

    // 클라이언트 연결 수락 및 스레드로 처리
    while (true) {
        SOCKET clientSocket = accept(serverSocket, nullptr, nullptr);
        if (clientSocket == INVALID_SOCKET) {
            cerr << "accept failed: " << WSAGetLastError() << endl;
            continue;
        }
        cout << "클라이언트 연결됨" <<   endl;
        thread t(handleClient, clientSocket);
        t.detach();
    }

    // 정리 (도달하지 않음)
    closesocket(serverSocket);
    WSACleanup();
    return 0;
}
