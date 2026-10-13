using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using BibleApp.Data;
using navigationshurely.models;
using navigationshurely.Services;

namespace navigationshurely.services
{
    public class Linguist
    {
        private State state;
        private ParseState parseState;
        private Command? command;
        private string commandBuffer = string.Empty;
        private string choiceCommandBuffer = string.Empty;
        private int quantifier = 0;
        private EntireCommandStatus validity;
        private State previousStateBeforeSearch;
        private char? activeFilter;
        private string searchQuery = string.Empty;
        private string wholeQueryWithCommas;
        private string commandString = string.Empty;
        private bool normalChoosingMode = true;
        public event Action<bool>? SearchUiVisibilityChanged;

        public Linguist()
        {
            state = State.B;
            parseState = ParseState.Start;
            command = null;
            validity = EntireCommandStatus.Incomplete;
            wholeQueryWithCommas = ",,,,,lt,os,mf";
        }

        public string CurrentCommandInfo()
        {
            String result = commandString;
            if (validity == EntireCommandStatus.Incomplete) 
            {
                return result;
            }
            commandString = "";
            return result;
        }

        private void AddCharToCommandInfo(char k)
        {
            switch (k)
            {
                case '{':
                    break;
                case '+':
                    commandString += "[Tab]";
                    break;
                case '|':
                    commandString += "Esc.";
                    break;
                case '<':
                    commandString += "Ctrl+D";
                    break;
                case '>':
                    commandString += "Ctrl+U";
                    break;
                default:
                    commandString += k;
                    break;
            }
        }

        public Color CommandColor()
        {
            switch (validity)
            {
                case EntireCommandStatus.Incomplete:
                    return Color.FromRgb(85, 200, 208);
                case EntireCommandStatus.Invalid:
                    return Color.FromRgb(255, 107, 120);
                case EntireCommandStatus.Valid:
                    return Color.FromRgb(142, 214, 165);
                default:
                    return Color.FromRgb(197, 206, 216);
            }
        }


        private static readonly Dictionary<char, int> Quantifiers = new()
        {
            ['z'] = 0,
            ['u'] = 1,
            ['d'] = 2,
            ['t'] = 3,
            ['q'] = 4,
            ['a'] = 4,
            ['i'] = 5,
            ['s'] = 6,
            ['e'] = 7,
            ['o'] = 8,
            ['n'] = 9
        };


        private static readonly Dictionary<string, int> BCommands = new()
        {
            ["ge"] = 1, //genesis
            ["x"] = 2, //exodo
            ["le"] = 3, //levitico
            ["bi"] = 4, //bilang
            ["de"] = 5, //deuteronomio
            ["su"] = 6, //josue
            ["uk"] = 7, //hukom
            ["ru"] = 8, //ruth
            ["is"] = 9, //I samuel
            ["ws"] = 10, // II samuel
            ["ih"] = 11, //I hari
            ["wh"] = 12, //II hari
            ["icr"] = 13, //I cronica
            ["wcr"] = 14, //II cronica
            ["ez"] = 15, //ezra
            ["ne"] = 16, //nehemias
            ["es"] = 17, //esther
            ["ob"] = 18, //job
            ["aw"] = 19, //awit
            ["wi"] = 20, //kawikaan
            ["ec"] = 21, //eclesiastes
            ["so"] = 22, //awit ng mga awit
            ["sa"] = 23, //isaias
            ["re"] = 24, //jeremias
            ["y"] = 25, //panaghoy
            ["ek"] = 26, //ezekiel
            ["da"] = 27, //daniel
            ["os"] = 28, //oseas
            ["oe"] = 29, //joel
            ["am"] = 30, //amos
            ["ba"] = 31, //obadias
            ["on"] = 32, //jonas
            ["mi"] = 33, //mikas
            ["na"] = 34, //nahum
            ["ha"] = 35, //habacuc
            ["ze"] = 36, //zefanias
            ["ag"] = 37, //hagai
            ["za"] = 38, //zacarias
            ["mal"] = 39, //malakias
            ["mat"] = 40, //mateo
            ["mar"] = 41, //marcos
            ["lu"] = 42, //lucas
            ["ua"] = 43, //juan
            ["gw"] = 44, //gawa
            ["ro"] = 45, //roma
            ["ico"] = 46, //I corinto
            ["wco"] = 47, //II corinto
            ["ga"] = 48, //galacia
            ["ef"] = 49, //efeso
            ["fp"] = 50, //filipos
            ["ls"] = 51, //colosas
            ["its"] = 52, //I tesalonica
            ["wts"] = 53, //II tesalonica
            ["itm"] = 54, //I timoteo
            ["wtm"] = 55, //II timoteo
            ["ti"] = 56, //tito
            ["fm"] = 57, //filemon
            ["he"] = 58, //hebreo
            ["sn"] = 59, //santiago
            ["p"] = 60, //I pedro
            ["wp"] = 61, //II pedro
            ["iua"] = 62, //I juan
            ["wua"] = 63, //II juan
            ["tua"] = 64, //III juan
            ["ud"] = 65, //judas
            ["ap"] = 66 //apocalipsis
        };


        private static readonly HashSet<string> BPrefixes = BuildPrefixes();

        private static HashSet<string> BuildPrefixes()
        {
            var prefixes = new HashSet<string>();

            foreach (var command in BCommands.Keys)
            {
                for (int i = 1; i < command.Length; i++)
                {
                    prefixes.Add(command[..i]);
                }
            }

            return prefixes;
        }


        private static readonly Dictionary<string, int> SCommands = new()
        {
            ["nkam"] = 54003015, //I timoteo 3:15
            ["nhsm"] = 59001006, //santiago 1:6
            ["akwp"] = 58011006, //hebreo 11:6
            ["ahnm"] = 20015008, //kawikaan 15:8
            ["agdn"] = 45008026, //romans 8:26
            ["knsk"] = 41011024, //mark 11:24
            ["aian"] = 62005014, //I juan 5:14
            ["alss"] = 40026039, //mateo 26:39
            ["mkps"] = 51004002, //colossas 4:2
            ["asas"] = 40008008, //mateo 8:8
            ["sapa"] = 19100005, //awit 100:5
            ["mtks"] = 48006002, //galacia 6:2
            ["pmss"] = 43017017, //juan 17:17
            ["knak"] = 45007012, //roma 7:12
            ["ssca"] = 45010004, //roma 10:4
            ["sttn"] = 52004007, //I tesalonica 4:7
            ["spsp"] = 54004005, //I timoteo 4:5
            ["swsm"] = 42001037, //lucas 1:37
            ["ddmn"] = 40028019, //mateo 28:19
            ["nins"] = 40028020, //mateo 28:20
            ["kinn"] = 46014037, //I corinto 14:37
            ["gnal"] = 50002014, //filipos 2:14
            ["samb"] = 45001020, //roma 1:20
            ["kina"] = 59001021, //santiago 1:21
            ["kymn"] = 43015003, //juan 15:3
            ["aapn"] = 43015005, //juan 15:5
            ["kmsa"] = 43015004, //juan 15:4
            ["adnd"] = 52002013, //I tesalonica 2:13
            ["ahat"] = 49001016, //efeso 1:16
            ["nkls"] = 52001002, //I tesalonica 1:2
            ["mnks"] = 59005016, //santiago 5:16
            ["nmks"] = 49006018, //efeso 6:18
            ["uusl"] = 54002001, //I timoteo 2:1
            ["hwmp"] = 20003007, //kawikaan 3:7
            ["knst"] = 48006010, //galacia 6:10
            ["dasm"] = 62003017, //I juan 3:17
            ["kmns"] = 62006002, //I juan 5:17
            ["nbas"] = 59005013, //santiago 5:13
            ["amsk"] = 24030019, //jeremias 30:19
            ["sgnl"] = 43003016, //juan 3:16
            ["apna"] = 66021004, //apocalipsis 21:4
            ["aaaa"] = 66001008, //apocalipsis 1:8
            ["kpnd"] = 46001027, //I corinto 1:27
            ["mamk"] = 51003022, //colosas 3:22
            ["amnk"] = 23033006, //isaiah 33:6
            ["sata"] = 20001024, //kawikaan 1:24
            ["aksk"] = 20022004, //kawikaan 22:4
            ["kaab"] = 14007014, //II cronica 7:14

            ["npan"] = 1001001, //Genesis 1:1
            ["agki"] = 1012002, //Genesis 12:2
            ["iknp"] = 2014014, //Exodo 14:14
            ["hkmn"] = 2020003, //Exodo 20:3
            ["imai"] = 2020012, //Exodo 20:12
            ["hkmo"] = 3019018, //Levitico 19:18
            ["pknn"] = 4006024, //Mga Bilang 6:24
            ["pnnp"] = 4006025, //Mga Bilang 6:25
            ["innp"] = 4006026, //Mga Bilang 6:26
            ["aiia"] = 5006005, //Deuteronomio 6:5
            ["kmam"] = 5031006, //Deuteronomio 31:6
            ["aani"] = 6001008, //Josue 1:8
            ["hbki"] = 6001009, //Josue 1:9
            ["akin"] = 6024015, //Josue 24:15
            ["asnr"] = 8001016, //Ruth 1:16
            ["nsnp"] = 9016007, //I Samuel 16:7
            ["nknm"] = 18042002, //Job 42:2
            ["matn"] = 19001001, //Awit 1:1
            ["apaa"] = 19023001, //Awit 23:1
            ["obal"] = 19023004, //Awit 23:4
            ["ibah"] = 19027004, //Awit 27:4
            ["oita"] = 19034008, //Awit 34:8
            ["apam"] = 19034018, //Awit 34:18
            ["mkns"] = 19037004, //Awit 37:4
            ["alnt"] = 19037023, //Awit 37:23
            ["adaa"] = 19046001, //Awit 46:1
            ["kama"] = 19046010, //Awit 46:10
            ["lman"] = 19051010, //Awit 51:10
            ["spam"] = 19056003, //Awit 56:3
            ["stsl"] = 19091001, //Awit 91:1
            ["asts"] = 19091002, //Awit 91:2
            ["ktkn"] = 19091004, //Awit 91:4
            ["anna"] = 19100003, //Awit 100:3
            ["pmap"] = 19103001, //Awit 103:1
            ["kgal"] = 19103012, //Awit 103:12
            ["iaan"] = 19118024, //Awit 118:24
            ["spln"] = 19119009, //Awit 119:9
            ["asma"] = 19119011, //Awit 119:11
            ["asmi"] = 19119105, //Awit 119:105
            ["ikaa"] = 19121001, //Awit 121:1
            ["assa"] = 19121002, //Awit 121:2
            ["minp"] = 19127001, //Awit 127:1
            ["mnnp"] = 19133001, //Awit 133:1
            ["amsi"] = 19139014, //Awit 139:14
            ["tksp"] = 20003005, //Kawikaan 3:5
            ["kmss"] = 20003006, //Kawikaan 3:6
            ["apsp"] = 20009010, //Kawikaan 9:10
            ["apah"] = 20010012, //Kawikaan 10:12
            ["imsp"] = 20016003, //Kawikaan 16:3
            ["apnt"] = 20016009, //Kawikaan 16:9
            ["akan"] = 20016018, //Kawikaan 16:18
            ["mmsa"] = 20016024, //Kawikaan 16:24
            ["akau"] = 20017017, //Kawikaan 17:17
            ["apnp"] = 20018010, //Kawikaan 18:10
            ["kaba"] = 20018021, //Kawikaan 18:21
            ["mmks"] = 20019021, //Kawikaan 19:21
            ["tmab"] = 20022006, //Kawikaan 22:6
            ["aban"] = 20027017, //Kawikaan 27:17
            ["imnb"] = 20031010, //Kawikaan 31:10
            ["iawn"] = 21012013, //Eclesiastes 12:13
            ["ssaa"] = 23009006, //Isaias 9:6
            ["iiss"] = 23026003, //Isaias 26:3
            ["nsns"] = 23040031, //Isaias 40:31
            ["hkms"] = 23041010, //Isaias 41:10
            ["piad"] = 23043002, //Isaias 43:2
            ["nsnd"] = 23053005, //Isaias 53:5
            ["wang"] = 23054017, //Isaias 54:17
            ["saam"] = 23055008, //Isaias 55:8
            ["snka"] = 24029011, //Jeremias 29:11
            ["apan"] = 24031003, //Jeremias 31:3
            ["tksa"] = 24033003, //Jeremias 33:3
            ["smkn"] = 25003022, //Panaghoy 3:22
            ["amyb"] = 25003023, //Panaghoy 3:23
            ["kisi"] = 33006008, //Mikas 6:8
            ["nakk"] = 35002004, //Habakuk 2:4
            ["sbap"] = 35003017, //Habakuk 3:17
            ["apmd"] = 36003017, //Sofonias 3:17
            ["dnab"] = 39003010, //Malakias 3:10
            ["mamm"] = 40005003, //Mateo 5:3
            ["mans"] = 40005004, //Mateo 5:4
            ["mana"] = 40005006, //Mateo 5:6
            ["kain"] = 40005014, //Mateo 5:14
            ["lnga"] = 40005016, //Mateo 5:16
            ["dsks"] = 40005044, //Mateo 5:44
            ["dipi"] = 40006006, //Mateo 6:6
            ["mnkn"] = 40006009, //Mateo 6:9
            ["kmkn"] = 40006020, //Mateo 6:20
            ["sksn"] = 40006021, //Mateo 6:21
            ["mnam"] = 40006026, //Mateo 6:26
            ["khkm"] = 40006031, //Mateo 6:31
            ["dhmn"] = 40006033, //Mateo 6:33
            ["mkak"] = 40007007, //Mateo 7:7
            ["sabh"] = 40007008, //Mateo 7:8
            ["knln"] = 40007012, //Mateo 7:12
            ["msak"] = 40011028, //Mateo 11:28
            ["nmsn"] = 40016024, //Mateo 16:24
            ["apnj"] = 40019026, //Mateo 19:26
            ["alnm"] = 40021022, //Mateo 21:22
            ["assk"] = 40022037, //Mateo 22:37
            ["iada"] = 40022038, //Mateo 22:38
            ["aapk"] = 40022039, //Mateo 22:39
            ["alaa"] = 40024035, //Mateo 24:35
            ["asah"] = 40025040, //Mateo 25:40
            ["alsj"] = 40028018, //Mateo 28:18
            ["pnjs"] = 41010027, //Marcos 10:27
            ["saan"] = 41010045, //Marcos 10:45
            ["aima"] = 41012030, //Marcos 12:30
            ["apai"] = 41012031, //Marcos 12:31
            ["asns"] = 41016015, //Marcos 16:15
            ["akaa"] = 42006031, //Lucas 6:31
            ["mkgn"] = 42006036, //Lucas 6:36
            ["apns"] = 42010027, //Lucas 10:27
            ["asks"] = 42011009, //Lucas 11:9
            ["dmam"] = 42012007, //Lucas 12:7
            ["gmhn"] = 42012031, //Lucas 12:31
            ["dsna"] = 42018027, //Lucas 18:27
            ["asnj"] = 42023034, //Lucas 23:34
            ["asjn"] = 42023046, //Lucas 23:46
            ["npsa"] = 43001001, //Juan 1:1
            ["anav"] = 43001014, //Juan 1:14
            ["ssja"] = 43003003, //Juan 3:3
            ["shsn"] = 43003017, //Juan 3:17
            ["adae"] = 43004024, //Juan 4:24
            ["mnns"] = 43008012, //Juan 8:12
            ["snnj"] = 43008031, //Juan 8:31
            ["hpam"] = 43010010, //Juan 10:10
            ["aamp"] = 43010011, //Juan 10:11
            ["sskn"] = 43011025, //Juan 11:25
            ["aasn"] = 43011026, //Juan 11:26
            ["ibua"] = 43013034, //Juan 13:34
            ["hmai"] = 43014001, //Juan 14:1
            ["aaai"] = 43014013, //Juan 14:13
            ["kkmn"] = 43014014, //Juan 14:14
            ["kaii"] = 43014015, //Juan 14:15
            ["akai"] = 43014027, //Juan 14:27
            ["kkms"] = 43015007, //Juan 15:7
            ["iaau"] = 43015012, //Juan 15:12
            ["wmld"] = 43015013, //Juan 15:13
            ["ahnh"] = 43015016, //Juan 15:16
            ["ambn"] = 43016033, //Juan 16:33
            ["aiab"] = 43017003, //Juan 17:3
            ["dtna"] = 44001008, //Gawa 1:8
            ["askm"] = 44004012, //Gawa 4:12
            ["aksm"] = 44016031, //Gawa 16:31
            ["nhas"] = 44020035, //Gawa 20:35
            ["shki"] = 45001016, //Roma 1:16
            ["sala"] = 45003023, //Roma 3:23
            ["dind"] = 45005008, //Roma 5:8
            ["sakn"] = 45006023, //Roma 6:23
            ["nnwn"] = 45008001, //Roma 8:1
            ["snkn"] = 45008018, //Roma 8:18
            ["annn"] = 45008028, //Roma 8:28
            ["aaas"] = 45008031, //Roma 8:31
            ["snhi"] = 45008032, //Roma 8:32
            ["hksl"] = 45008037, //Roma 8:37
            ["sanl"] = 45008038, //Roma 8:38
            ["kakk"] = 45008039, //Roma 8:39
            ["skim"] = 45010009, //Roma 10:9
            ["satn"] = 45010010, //Roma 10:10
            ["knmk"] = 45012001, //Roma 12:1
            ["ahkm"] = 45012002, //Roma 12:2
            ["mspm"] = 45012012, //Roma 12:12
            ["hkps"] = 45012021, //Roma 12:21
            ["pnkn"] = 45015013, //Roma 15:13
            ["hdsi"] = 46010013, //I Corinto 10:13
            ["labl"] = 46013007, //I Corinto 13:7
            ["hkpa"] = 46015033, //I Corinto 15:33
            ["snks"] = 47005007, //II Corinto 5:7
            ["kkas"] = 47005017, //II Corinto 5:17
            ["ansk"] = 48002020, //Galacia 2:20
            ["dabn"] = 48005022, //Galacia 5:22
            ["kpls"] = 48005023, //Galacia 5:23
            ["ahtm"] = 48006009, //Galacia 6:9
            ["dmns"] = 48006014, //Galacia 6:14
            ["ssbk"] = 49002008, //Efeso 2:8
            ["nsmg"] = 49003020, //Efeso 3:20
            ["amks"] = 49004032, //Efeso 4:32
            ["skmk"] = 49006010, //Efeso 6:10
            ["mknb"] = 49006011, //Efeso 6:11
            ["saap"] = 49006012, //Efeso 6:12
            ["mkls"] = 50004004, //Filipos 4:4
            ["aakn"] = 50004007, //Filipos 4:7
            ["kmka"] = 50004008, //Filipos 4:8
            ["lnmb"] = 50004013, //Filipos 4:13
            ["mkwp"] = 52005017, //I Tesalonica 5:17
            ["slnm"] = 52005018, //I Tesalonica 5:18
            ["snal"] = 52005021, //I Tesalonica 5:21
            ["lnab"] = 52005022, //I Tesalonica 5:22
            ["shtb"] = 55001007, //II Timoteo 1:7
            ["pmhn"] = 55002015, //II Timoteo 2:15
            ["nanm"] = 55004007, //II Timoteo 4:7
            ["nknn"] = 59001005, //Santiago 1:5
            ["mksd"] = 59004008, //Santiago 4:8
            ["dkil"] = 60002009, //I Pedro 2:9
            ["kiab"] = 60003015, //I Pedro 3:15
            ["niis"] = 60005007, //I Pedro 5:7
            ["kmmk"] = 60005008, //I Pedro 5:8
            ["mmmt"] = 62004007, //I Juan 4:7
            ["ahua"] = 62004008, //I Juan 4:8
            ["aiap"] = 62005011, //I Juan 5:11
            ["akna"] = 62005012, //I Juan 5:12
            ["assm"] = 66022020, //Apocalipsis 22:20
            ["annd"] = 1001027, // Genesis 1:27
            ["annp"] = 1002007, // Genesis 2:7
            ["atsi"] = 1050020, // Genesis 50:20
            ["asnh"] = 18001021, // Job 1:21
            ["pnbt"] = 19150006, // Psalm 150:6
            ["sbtb"] = 21003001, // Ecclesiastes 3:1
            ["anka"] = 23006008, // Isaiah 6:8
            ["naad"] = 27003017, // Daniel 3:17
            ["nmyn"] = 27006023, // Daniel 6:23
            ["apym"] = 34001007, // Nahum 1:7
            ["nmys"] = 38004006, // Zechariah 4:6
            ["stks"] = 40018020, // Matthew 18:20
            ["sykd"] = 43003030, // John 3:30
            ["stal"] = 45010013, // Romans 10:13
            ["dtny"] = 46013013, // 1 Corinthians 13:13
            ["inai"] = 51003002, // Colossians 3:2
            ["ntta"] = 53003003, // 2 Thessalonians 3:3
            ["stas"] = 58004012, // Hebrews 4:12
            ["napa"] = 58011001, // Hebrews 11:1
            ["ktyn"] = 58012001, // Hebrews 12:1
            ["nmns"] = 58012002, // Hebrews 12:2
            ["dtmt"] = 59001022, // James 1:22
            ["adnr"] = 59001027, // James 1:27
            ["pnks"] = 59004007, // James 4:7
            ["dynn"] = 62003016, // 1 John 3:16
            ["nayn"] = 66003020, // Revelation 3:20
            ["aala"] = 1001002, // Genesis 1:2
            ["asnd"] = 1001003, // Genesis 1:3
            ["atnd"] = 1001005, // Genesis 1:5
            ["agnd"] = 1001007, // Genesis 1:7
            ["anal"] = 1002001, // Genesis 2:1
            ["ania"] = 1002002, // Genesis 2:2
            ["abnd"] = 1002003, // Genesis 2:3
            ["iapn"] = 1002004, // Genesis 2:4
            ["aana"] = 1003001, // Genesis 3:1
            ["asnb"] = 1003002, // Genesis 3:2
            ["dtsb"] = 1003003, // Genesis 3:3
            ["asna"] = 1003004, // Genesis 3:4
            ["anmn"] = 1003006, // Genesis 3:6
            ["snnp"] = 1012001, // Genesis 12:1
            ["apka"] = 1012003, // Genesis 12:3
            ["aink"] = 1050016, // Genesis 50:16
            ["gsnk"] = 1050017, // Genesis 50:17
            ["anak"] = 1050018, // Genesis 50:18
            ["knhk"] = 1050021, // Genesis 50:21
            ["aapa"] = 2014001, // Exodus 14:1
            ["smsm"] = 2014002, // Exodus 14:2
            ["asnf"] = 2014003, // Exodus 14:3
            ["ansh"] = 2014005, // Exodus 14:5
            ["ainf"] = 2014006, // Exodus 14:6
            ["aapm"] = 2020002, // Exodus 20:2
            ["hkgp"] = 2020004, // Exodus 20:4
            ["hmys"] = 2020005, // Exodus 20:5
            ["apkn"] = 2020006, // Exodus 20:6
            ["hmba"] = 2020007, // Exodus 20:7
            ["inau"] = 5006001, // Deuteronomy 6:1
            ["niam"] = 5006002, // Deuteronomy 6:2
            ["dmno"] = 5006003, // Deuteronomy 6:3
            ["dmoi"] = 5006004, // Deuteronomy 6:4
            ["aams"] = 5006006, // Deuteronomy 6:6
            ["nnpn"] = 6001001, // Joshua 1:1
            ["smna"] = 6001002, // Joshua 1:2
            ["msia"] = 6001004, // Joshua 1:4
            ["wmst"] = 6001005, // Joshua 1:5
            ["mkam"] = 6001006, // Joshua 1:6
            ["akka"] = 6024003, // Joshua 24:3
            ["aikk"] = 6024004, // Joshua 24:4
            ["aass"] = 6024005, // Joshua 24:5
            ["aika"] = 6024006, // Joshua 24:6
            ["ansy"] = 6024007, // Joshua 24:7
            ["kpas"] = 19023002, // Psalm 23:2
            ["kpaa"] = 19023003, // Psalm 23:3
            ["ipan"] = 19023005, // Psalm 23:5
            ["tnak"] = 19023006, // Psalm 23:6
            ["ndsa"] = 19027002, // Psalm 27:2
            ["stsk"] = 19027005, // Psalm 27:5
            ["anym"] = 19027006, // Psalm 27:6
            ["dmop"] = 19027007, // Psalm 27:7
            ["nish"] = 19027008, // Psalm 27:8
            ["apap"] = 19034001, // Psalm 34:1
            ["aaka"] = 19034002, // Psalm 34:2
            ["odnn"] = 19034003, // Psalm 34:3
            ["ahap"] = 19034004, // Psalm 34:4
            ["syns"] = 19034005, // Psalm 34:5
            ["hkmd"] = 19037001, // Psalm 37:1
            ["stsy"] = 19037002, // Psalm 37:2
            ["akpg"] = 19037006, // Psalm 37:6
            ["mknp"] = 19037008, // Psalm 37:8
            ["stam"] = 19037009, // Psalm 37:9
            ["ktht"] = 19046002, // Psalm 46:2
            ["miin"] = 19046004, // Psalm 46:4
            ["adan"] = 19046005, // Psalm 46:5
            ["amba"] = 19046006, // Psalm 46:6
            ["apnm"] = 19046007, // Psalm 46:7
            ["mksa"] = 19051001, // Psalm 51:1
            ["hmal"] = 19051002, // Psalm 51:2
            ["stkk"] = 19051003, // Psalm 51:3
            ["lsis"] = 19051004, // Psalm 51:4
            ["nayi"] = 19051005, // Psalm 51:5
            ["stki"] = 19091003, // Psalm 91:3
            ["iahm"] = 19091005, // Psalm 91:5
            ["dssn"] = 19091006, // Psalm 91:6
            ["ilam"] = 19091007, // Psalm 91:7
            ["imln"] = 19091008, // Psalm 91:8
            ["stio"] = 19091009, // Psalm 91:9
            ["wkms"] = 19091010, // Psalm 91:10
            ["kdks"] = 19091012, // Psalm 91:12
            ["iyal"] = 19091013, // Psalm 91:13
            ["syts"] = 19091015, // Psalm 91:15
            ["absn"] = 19091016, // Psalm 91:16
            ["nsnn"] = 19103003, // Psalm 103:3
            ["nstn"] = 19103004, // Psalm 103:4
            ["nsbs"] = 19103005, // Psalm 103:5
            ["kiak"] = 19103007, // Psalm 103:7
            ["syhg"] = 19103010, // Psalm 103:10
            ["msss"] = 19119001, // Psalm 119:1
            ["msnn"] = 19119002, // Psalm 119:2
            ["oshn"] = 19119003, // Psalm 119:3
            ["iisa"] = 19119004, // Psalm 119:4
            ["omna"] = 19119005, // Psalm 119:5
            ["apat"] = 19121005, // Psalm 121:5
            ["hksn"] = 19121006, // Psalm 121:6
            ["opis"] = 19139001, // Psalm 139:1
            ["inaa"] = 19139002, // Psalm 139:2
            ["isaa"] = 19139003, // Psalm 139:3
            ["stwp"] = 19139004, // Psalm 139:4
            ["ikas"] = 19139005, // Psalm 139:5
            ["akhm"] = 20003001, // Proverbs 3:1
            ["stkn"] = 20003002, // Proverbs 3:2
            ["hkpn"] = 20003003, // Proverbs 3:3
            ["mksi"] = 20003008, // Proverbs 3:8
            ["stak"] = 20003014, // Proverbs 3:14
            ["ampn"] = 20016001, // Proverbs 16:1
            ["gnpa"] = 20016004, // Proverbs 16:4
            ["spnk"] = 20016006, // Proverbs 16:6
            ["paml"] = 20016007, // Proverbs 16:7
            ["agnt"] = 20016011, // Proverbs 16:11
            ["ahah"] = 20018001, // Proverbs 18:1
            ["amaw"] = 20018002, // Proverbs 18:2
            ["pama"] = 20018003, // Proverbs 18:3
            ["amsn"] = 20018004, // Proverbs 18:4
            ["amln"] = 20018006, // Proverbs 18:6
            ["iaia"] = 23040001, // Isaiah 40:1
            ["mkmp"] = 23040002, // Isaiah 40:2
            ["atni"] = 23040003, // Isaiah 40:3
            ["oinn"] = 23040009, // Isaiah 40:9
            ["napd"] = 23040010, // Isaiah 40:10
            ["mksh"] = 23041001, // Isaiah 41:1
            ["snni"] = 23041002, // Isaiah 41:2
            ["khsa"] = 23041003, // Isaiah 41:3
            ["sgay"] = 23041004, // Isaiah 41:4
            ["nnmp"] = 23041005, // Isaiah 41:5
            ["ntny"] = 23043001, // Isaiah 43:1
            ["staa"] = 23043003, // Isaiah 43:3
            ["yian"] = 23043004, // Isaiah 43:4
            ["assh"] = 23043006, // Isaiah 43:6
            ["iiab"] = 23043008, // Isaiah 43:8
            ["snsa"] = 23053001, // Isaiah 53:1
            ["syha"] = 23053003, // Isaiah 53:3
            ["tlng"] = 23053006, // Isaiah 53:6
            ["syng"] = 23053007, // Isaiah 53:7
            ["agna"] = 23053009, // Isaiah 53:9
            ["inam"] = 24029001, // Jeremiah 29:1
            ["pnms"] = 24029002, // Jeremiah 29:2
            ["aiha"] = 24029007, // Jeremiah 29:7
            ["stga"] = 24029008, // Jeremiah 29:8
            ["akym"] = 24029012, // Jeremiah 29:12
            ["spys"] = 24031001, // Jeremiah 31:1
            ["mika"] = 24031004, // Jeremiah 31:4
            ["mkmn"] = 24031005, // Jeremiah 31:5
            ["stmn"] = 24031006, // Jeremiah 31:6
            ["nads"] = 24031008, // Jeremiah 31:8
            ["apsm"] = 40005001, // Matthew 5:1
            ["mams"] = 40005005, // Matthew 5:5
            ["mamp"] = 40005010, // Matthew 5:10
            ["mkpk"] = 40005011, // Matthew 5:11
            ["kaan"] = 40005013, // Matthew 5:13
            ["hrnp"] = 40005015, // Matthew 5:15
            ["hnia"] = 40005017, // Matthew 5:17
            ["ktas"] = 40005019, // Matthew 5:19
            ["nnns"] = 40005021, // Matthew 5:21
            ["ktki"] = 40005023, // Matthew 5:23
            ["imrs"] = 40005024, // Matthew 5:24
            ["mkas"] = 40005025, // Matthew 5:25
            ["mknh"] = 40006001, // Matthew 6:1
            ["knpi"] = 40006002, // Matthew 6:2
            ["dtpi"] = 40006003, // Matthew 6:3
            ["uaip"] = 40006004, // Matthew 6:4
            ["aspn"] = 40006007, // Matthew 6:7
            ["hnkm"] = 40006008, // Matthew 6:8
            ["dnak"] = 40006010, // Matthew 6:10
            ["aims"] = 40006012, // Matthew 6:12
            ["ahmk"] = 40006013, // Matthew 6:13
            ["dtkh"] = 40006015, // Matthew 6:15
            ["dtis"] = 40006017, // Matthew 6:17
            ["uhkm"] = 40006018, // Matthew 6:18
            ["hkmu"] = 40007001, // Matthew 7:1
            ["stsh"] = 40007002, // Matthew 7:2
            ["abmt"] = 40007003, // Matthew 7:3
            ["opsm"] = 40007004, // Matthew 7:4

        };


        private static readonly HashSet<string> SPrefixes = SemiColonPrefixes();

        private static HashSet<string> SemiColonPrefixes()
        {
            var prefixes = new HashSet<string>();

            foreach (var command in SCommands.Keys)
            {
                for (int i = 1; i < command.Length; i++)
                {
                    prefixes.Add(command[..i]);
                }
            }

            return prefixes;
        }

        public string ModeString()
        {
            if (state == State.B) return "BOOK Mode";
            else if (state == State.S) return "SEMI-COLON Mode";
            else if (state == State.C) return "CHAPTER Mode";
            else if (state == State.V) return "VERSE Mode";
            else if (state == State.Filter) return "SEARCH: FILTER mode";
            else if (state == State.Query) return "SEARCH: QUERY mode";
            else if (state == State.Choice)
            {
                if (normalChoosingMode)
                {
                    return "SEARCH: CHOICE mode";
                }
                else
                {
                    return "SEARCH: ABSOLUTE PATH mode";
                }
            }
            else return "Random mode";
        }

        private enum ParseState
        {
            Start,
            ReadingQuantifier,
            ReadingBookAlias,
            ReadingSemicolonAlias,
            SettingMark,
            TeleportingToMark,
            CopyingSomething,
            FilterDeciding,
            FilterBookAlias,
            FilterQuantifier,
        }

        public void ResetCommand()
        {
            parseState = ParseState.Start;
            quantifier = 0;
            this.command = null;
            commandBuffer = "";
        }

        private EntireCommandStatus NotValid()
        {
            validity = EntireCommandStatus.Invalid;
            ResetCommand();
            return validity;
        }

        public Command? TakeCommand()
        {
            Command? result = command;
            ResetCommand();
            return result;
        }

        //MAIN INPUT HANDLER
        public EntireCommandStatus Feed(char k)
        {
            AddCharToCommandInfo(k);
            //termination keys
            if (parseState == ParseState.TeleportingToMark)
            {
                command = new Command { Mode = State.S, Quantity = -1 * (int)k, Direction = Direct.Absolute };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            if (state == State.Query || state == State.Filter || state == State.Choice)
            {
                return Search(k);
            }
            if (k == 'x' && state != State.B && state != State.S)
            {
                ResetCommand();
                validity = EntireCommandStatus.Invalid;
                return validity;
            }
            else if (k == ' ')
            {
                if (state == State.C || state == State.V)
                {
                    return CompleteMovement(Direct.Absolute);
                }

                ResetCommand();
                validity = EntireCommandStatus.Invalid;
                return validity;
            }
            else if (k == '\'')
            {
                parseState = ParseState.TeleportingToMark;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }
            //book mode
            if (state == State.B)
            {
                commandBuffer += k;

                if (BCommands.TryGetValue(commandBuffer, out int position))
                {
                    command = new Command { Mode = State.B, Quantity = position, Direction = Direct.Absolute };
                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (BPrefixes.Contains(commandBuffer))
                {
                    parseState = ParseState.ReadingBookAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                
                //movement
                if (k == 'j' || k == 'k')
                {
                    return FeedMovement(k);
                }

                //mode switching in book state
                else if (k == 'c')
                {
                    state = State.C;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == 'v')
                {
                    state = State.V;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == ';')
                {
                    state = State.S;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (k == '/')
                {
                    previousStateBeforeSearch = state;
                    state = State.Query;
                    EnterQueryState();
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else
                {
                    return NotValid();
                }
            }

            //mode switching
            if (parseState == ParseState.Start)
            {
                switch(k)
                {
                    case 'b':
                        state = State.B;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'c':
                        state = State.C;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'v':
                        state = State.V;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case ';':
                        state = State.S;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case '/':
                        previousStateBeforeSearch = state;
                        state = State.Query;
                        EnterQueryState() ;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    default:
                        break;
                }
            }

            //semi colon mode
            if (state == State.S)
            {
                commandBuffer += k;

                //checks if it is calling mark functions
                if (parseState == ParseState.SettingMark)
                {
                    command = new Command { Mode = state, Quantity = (int)k, Direction = Direct.Absolute };
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (parseState == ParseState.CopyingSomething)
                {
                    switch (k)
                    {
                        case 't':
                            return CopyTheTitle();
                        case 'v':
                            return CopyTheVerse();
                        default:
                            return NotValid();
                    }
                }

                //trying to get S-state aliases
                if (SCommands.TryGetValue(commandBuffer, out int position))
                {
                    command = new Command
                    {
                        Mode = State.S,
                        Quantity = position,
                        Direction = Direct.Absolute
                    };

                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (SPrefixes.Contains(commandBuffer))
                {
                    parseState = ParseState.ReadingSemicolonAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //One letter, sub-state switching commands
                if (parseState == ParseState.Start)
                {
                    switch (k)
                    {
                        case 'r':
                            return HistoryCommands(k);
                        case 'z':
                            return HistoryCommands(k);
                        case 'q':
                            parseState = ParseState.SettingMark;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case 'x':
                            return TranslationChange();
                        case 'e':
                            return ToggleUI();
                        case 'f':
                            return HelpWasRequested();
                        case 'y':
                            parseState = ParseState.CopyingSomething;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case '+':
                            return BackgroundChange();
                        default:
                            break;
                    }
                }

                return NotValid();
            }

            //chapter and verse mode
            if (state == State.C || state == State.V)
            {
                return FeedMovement(k);
            }

            //unexpected error has occurred (eg. a user pressed a random key)
            return NotValid();
        }

        private EntireCommandStatus BackgroundChange()
        {
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 700 };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus Search(char k)
        {
            //toggles on and off SEARCH mode
            if (k == '/')
            {
                SearchUiVisibilityChanged?.Invoke(false); // Close search UI
                state = previousStateBeforeSearch;
                commandString = "";
                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //tab character (switching focused ui's)
            else if (k == '+')
            {
                commandString = "";
                state = state switch
                {
                    State.Query => State.Filter,
                    State.Filter => State.Choice,
                    State.Choice => State.Query,
                    _ => State.Query
                };
                if (state == State.Query)
                {
                    EnterQueryState();
                }
                
                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //query state
            if (state == State.Query)
            { 
                commandString = searchQuery;
                if (k == '_')
                {
                    //process the search using whole query
                    state = State.Filter;
                    commandString = "";
                    AddFilter("", searchQuery, false);
                    return ShipOutProperSearchCommand();
                }
                else if (k == '{')
                {
                    if (searchQuery == "")
                    {
                        return NotValid();
                    }
                    searchQuery = searchQuery[..^1];
                    commandString = searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                else if (k == '<' || k == '>' || k == '|')
                {
                    validity = EntireCommandStatus.Invalid;
                    return validity;
                }
                else
                {
                    searchQuery += k;
                    commandString = searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
            }

            //filter state
            else if (state == State.Filter)
            {
                if (k == '_')
                {
                    return ShipOutProperSearchCommand();
                }
                //choosing which filter the user wants to change
                if (parseState == ParseState.Start && activeFilter == null)
                {
                    switch (k)
                    {
                        case 'b':
                            parseState = ParseState.FilterBookAlias;
                            activeFilter = k;
                            break;
                        case 'c':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 'v':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 't':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'l':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'o':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'm':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        default:
                            return NotValid();
                    }
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //actually editing the filter now
                else if (activeFilter != null)
                {
                    if (parseState == ParseState.FilterBookAlias)
                    {
                        if (k == ' ')
                        {
                            AddFilter("b", " ", true);
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }
                        commandBuffer += k;

                        if (BCommands.TryGetValue(commandBuffer, out int position))
                        {
                            //adding and/or replacing book filter
                            string filterOption = position.ToString();
                            AddFilter("b", filterOption, false);

                            commandBuffer = "";
                            parseState = ParseState.Start;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }

                        if (BPrefixes.Contains(commandBuffer))
                        {
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        }
                        activeFilter = null;
                        return NotValid();
                    }
                    else if (parseState == ParseState.FilterQuantifier)
                    {
                        return FinishFilterQuantifier(k, activeFilter.ToString());
                    }
                    else if (parseState == ParseState.FilterDeciding)
                    {
                        switch (activeFilter)
                        {
                            case 't':
                                if (k == 'o' || k == 'n')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else if (k == ' ')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), true);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'l':
                                if (k == 'e' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'o':
                                if (k == 'a' || k == 's')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'm':
                                if (k == 'f' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                        }
                    }
                    else
                    {
                        activeFilter = null;
                        validity = EntireCommandStatus.Invalid;
                        return validity;
                    }
                }
                activeFilter = null;
                return NotValid();
            }
            else if (state == State.Choice)
            {
                if (normalChoosingMode)
                {
                    commandString = "";

                    if (parseState != ParseState.Start && parseState != ParseState.ReadingQuantifier)
                    {
                        if (k == 'g')
                        {
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }

                        return NotValid();
                    }

                    switch (k)
                    {
                        case '_':
                            FoundTheSearchedVerse();
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '|':
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case '<':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '>':
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'g':
                            parseState = ParseState.FilterBookAlias;
                            commandString += k;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case 'G':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'i':
                            normalChoosingMode = false;
                            commandString = choiceCommandBuffer;
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'c':
                            normalChoosingMode = false;
                            choiceCommandBuffer = "";
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        default:
                            return FeedMovement(k);
                    }
                }
                else
                {
                    
                    //filtering out choices mode
                    switch (k)
                    {
                        case '|':
                            normalChoosingMode = true;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        case '<':
                        case '>':
                            break;
                        case '{':
                            if (choiceCommandBuffer != "")
                            {
                                choiceCommandBuffer = choiceCommandBuffer[..^1];
                            }
                            break;
                        default:
                            choiceCommandBuffer += k;
                            break;
                    }
                    return ShipOutChoiceCommand(choiceCommandBuffer);
                }
            }
            else
            {
                activeFilter = null;
                return NotValid();
            }
        }

        private EntireCommandStatus FinishFilterQuantifier(char k, string whichFilter)
        {
            {
                if (quantifier == 0 && k == ' ')
                {
                    AddFilter(activeFilter.ToString(), " ", true);
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                //user (may have) typed an alias to a number
                if (Quantifiers.TryGetValue(k, out int digit))
                {
                    quantifier = (quantifier) * 10 + digit;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //user typed an actual number
                if (char.IsDigit(k))
                {
                    quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //must be a movement-terminated command
                //if (quantifier == 0) { quantifier = 1; }

                if (k == 'j' || k == 'k' || k == ' ')
                {
                    string quantifierString = quantifier.ToString();
                    string optionString = quantifierString + k.ToString();

                    AddFilter(whichFilter, optionString, false); 
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else
                {
                    activeFilter = null;
                    return NotValid();
                }
            }
        }

        private void AddFilter(string whichFilter, string option, bool weAreDeleting)
        {
            string[] filterParts = wholeQueryWithCommas.Split(',');
            string newFilter = whichFilter + option;
            int filterIndex;
            switch (whichFilter)
            {
                case "b":
                    filterIndex = 1;
                    break;
                case "c":
                    filterIndex = 2;
                    break;
                case "v":
                    filterIndex = 3;
                    break;
                case "t":
                    filterIndex = 4;
                    break;
                case "l":
                    filterIndex = 5;
                    break;
                case "o":
                    filterIndex = 6;
                    break;
                case "m":
                    filterIndex = 7;
                    break;
                default:
                    filterIndex = 0;
                    break;
            }
            if (weAreDeleting)
            {
                filterParts[filterIndex] = "";
            }
            else
            {
                filterParts[filterIndex] = newFilter;
            }
            activeFilter = null;
            wholeQueryWithCommas = string.Join(',', filterParts);
            AFilterWasChanged();
        }

        private EntireCommandStatus HistoryCommands(char k)
        {
            if (k == 'r')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Up };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            if (k == 'z')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Down };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            validity = EntireCommandStatus.Invalid;
            return validity;
        }

        private EntireCommandStatus FeedMovement(char k)
        {
            //user (may have) typed an alias to a number
            if (Quantifiers.TryGetValue(k, out int digit)) 
            {
                quantifier = (quantifier) * 10 + digit;
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //user typed an actual number
            if (char.IsDigit(k))
            {
                quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //must be a movement-terminated command
            if (quantifier == 0) { quantifier = 1; }

            if (k == 'j')
            {
                return CompleteMovement(Direct.Down);
            }
            if (k == 'k')
            {
                return CompleteMovement(Direct.Up);
            }

            //shouldn't reach here
            return NotValid();
        }

        private EntireCommandStatus CompleteMovement(Direct direction)
        {
            if (quantifier != 0)
            {
                command = new Command
                {
                    Mode = state,
                    Quantity = quantifier,
                    Direction = direction
                };
                parseState = ParseState.Start;
                quantifier = 0;

                validity = EntireCommandStatus.Valid;
                return validity;
            }

            validity = EntireCommandStatus.Invalid;
            return validity;
        }

        private EntireCommandStatus TranslationChange()
        {
            command = new Command
            {
                Mode = state,
                Quantity = 200,
                Direction = Direct.Absolute
            };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus ToggleUI()
        {
            command = new Command { Mode = state, Quantity = 300, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus HelpWasRequested()
        {
            command = new Command { Mode = state, Quantity = 400, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }
         
        private EntireCommandStatus CopyTheVerse()
        {
            command = new Command { Mode = state, Quantity = 500, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus CopyTheTitle()
        {
            command = new Command { Mode = state, Quantity = 600, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void AFilterWasChanged()
        {
            command = new Command { Mode = State.Query, Direction = Direct.Absolute, Quantity = 1, SearchText = wholeQueryWithCommas};
        }

        private EntireCommandStatus ShipOutProperSearchCommand()
        {
            command = new Command { Mode = State.Query, Quantity = 0, Direction = Direct.Absolute, SearchText = wholeQueryWithCommas };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void EnterQueryState()
        {
            SearchUiVisibilityChanged?.Invoke(true);  // Open search UI
            commandString = searchQuery;
        }

        private void FoundTheSearchedVerse()
        {
            command = new Command { Mode = State.Choice, Direction = Direct.Absolute, Quantity = 0 };
        }

        private EntireCommandStatus ShipOutChoiceCommand(string  searchText)
        {
            commandString = searchText;
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 1, SearchText = searchText };
            validity = EntireCommandStatus.Valid;
            return validity;
        }
    }
}
