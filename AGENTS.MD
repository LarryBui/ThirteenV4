* always run unit test
* For Client changes: only build Assembly-CSharp-Editor without dependencies (dotnet build Client/Assembly-CSharp-Editor.csproj --no-dependencies)
* For Server changes: always rebuild nakama and refresh docker (docker-compose up --build -d nakama)
* always add documentation to code
* do not change code until explicitly approved
* once your proposal is approved, you have full permission to make changes on what has been approved; no need to ask after each command