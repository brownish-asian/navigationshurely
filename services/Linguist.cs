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
            ["stal"] = 45010013, // Romans 10:13
            ["inai"] = 51003002, // Colossians 3:2
            ["ntta"] = 53003003, // 2 Thessalonians 3:3
            ["stas"] = 58004012, // Hebrews 4:12
            ["napa"] = 58011001, // Hebrews 11:1
            ["nmns"] = 58012002, // Hebrews 12:2
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
            ["nnns"] = 40005021, // Matthew 5:21
            ["imrs"] = 40005024, // Matthew 5:24
            ["mkas"] = 40005025, // Matthew 5:25
            ["mknh"] = 40006001, // Matthew 6:1
            ["knpi"] = 40006002, // Matthew 6:2
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
            ["smid"] = 54002005, // I Timoteo 2:5 — Sapagka't may isang Dios at may isang Tagapamagitan sa Dios at sa mga tao, ang taong si Cristo Jesus,
            ["nnsm"] = 40005038, // Mateo 5:38 — Narinig ninyong sinabi, Mata sa mata, at ngipin sa ngipin:
            ["ansu"] = 41010017, // Marcos 10:17 — At nang siya'y umalis na lumalakad sa daan, ay may isang tumakbong lumapit sa kaniya, at lumuhod sa harap niya, at siya'y tinanong, Mabuting Guro, ano ang gagawin ko upang ako'y magmana ng buhay na walang hanggan?
            ["ssnt"] = 42006014, // Lucas 6:14 — Si Simon, na tinawag naman niyang Pedro, at si Andres na kaniyang kapatid, at si Santiago at si Juan, at si Felipe at si Bartolome.
            ["psnj"] = 43001015, // Juan 1:15 — Pinatotohanan siya ni Juan, at sumigaw, na nagsasabi, Ito yaong aking sinasabi, Ang pumaparitong nasa hulihan ko ay magiging una sa akin: sapagka't siya'y una sa akin.
            ["anma"] = 44001015, // Gawa 1:15 — At nang mga araw na ito'y nagtindig si Pedro sa gitna ng mga kapatid, at nagsabi (at nangagkakatipon ang karamihang mga tao, na may isang daa't dalawangpu),
            ["akla"] = 45003013, // Roma 3:13 — Ang kanilang lalamunan ay isang libingang bukas; Nagsigamit ng daya sa pamamagitan ng kanilang mga dila: Ang kamandag ng mga ulupong ang siyang nasa ilalim ng kanilang mga labi:
            ["nbsc"] = 46001013, // I Corinto 1:13 — Nabahagi baga si Cristo? ipinako baga sa krus si Pablo dahil sa inyo? o binautismuhan baga kayo sa pangalan ni Pablo?
            ["saln"] = 47004015, // II Corinto 4:15 — Sapagka't ang lahat ng mga bagay ay dahil sa inyo, upang ang biyaya na pinarami sa pamamagitan ng marami, ay siyang magpasagana ng pagpapasalamat sa ikaluluwalhati ng Dios.
            ["sbna"] = 48002012, // Galacia 2:12 — Sapagka't bago nagsidating ang ilang mula kay Santiago, ay nakisalo siya sa mga Gentil; nguni't nang sila'y magsidating na, siya'y umurong, at humiwalay sa mga Gentil, palibhasa'y natatakot sa mga sa pagtutuli.
            ["ddar"] = 49001015, // Efeso 1:15 — Dahil dito ako rin naman, pagkarinig ng pananampalataya sa Panginoong Jesus na nasa inyo, at ng pagibig na inyong ipinakita sa lahat ng mga banal,
            ["nikn"] = 50001012, // Filipos 1:12 — Ngayon ibig ko na inyong maalaman, mga kapatid, na ang mga bagay na nangyari sa akin ay nangyari sa lalong ikasusulong ng evangelio;
            ["nnsa"] = 51001012, // Colosas 1:12 — Na nagpapasalamat sa Ama, na nagpaging dapat sa atin upang makabahagi sa mana ng mga banal sa kaliwanagan;
            ["sisn"] = 52004015, // I Tesalonica 4:15 — Sapagka't ito'y sinasabi namin sa inyo sa salita ng Panginoon, na tayong nangabubuhay, na nangatitira hanggang sa pagparito ng Panginoon, ay hindi tayo mangauuna sa anomang paraan sa nangatutulog.
            ["atsa"] = 54001014, // I Timoteo 1:14 — At totoong sumagana ang biyaya ng ating Panginoon na nasa pananampalataya at pagibig na pawang kay Cristo Jesus.
            ["inmn"] = 55001015, // II Timoteo 1:15 — Ito'y nalalaman mo, na nagsihiwalay sa akin ang lahat ng nangasa Asia; na sa mga yaon ay si Figello at si Hermogenes.
            ["apia"] = 56001013, // Tito 1:13 — Ang patotoong ito ay tunay. Dahil dito'y sawayin mong may kabagsikan sila, upang mangapakagaling sa pananampalataya,
            ["smsg"] = 57001015, // Filemon 1:15 — Sapagka't marahil sa ganito siya'y nahiwalay sa iyo sa sangdaling panahon, upang siya'y mapasa iyo magpakailan man;
            ["mntn"] = 58004011, // Hebreo 4:11 — Magsipagsikap nga tayo ng pagpasok sa kapahingahang yaon, upang huwag marapa ang sinoman ayon sa gayong halimbawa ng pagsuway.
            ["kmak"] = 59001015, // Santiago 1:15 — Kung magkagayo'y ang kahalayan, kung maipaglihi ay nanganganak ng kasalanan: at ang kasalanan, pagka malaki na ay namumunga ng kamatayan.
            ["nisk"] = 60001012, // I Pedro 1:12 — Na ipinahayag sa kanila, na hindi sa ganang kanilang sarili, kundi sa ganang inyo, pinangasiwaan nila ang mga bagay na ito, na ngayo'y ibinalita sa inyo sa pamamagitan ng mga nagsipangaral sa inyo ng evangelio sa pamamagitan ng Espiritu Santo na sinugo mula sa langit; na ang mga bagay na ito'y ninanasang mamasdan ng mga anghel.
            ["aikm"] = 61001013, // II Pedro 1:13 — At inaakala kong matuwid, na samantalang ako'y nasa tabernakulong ito, ay kilusin ko kayo na ipaalaala ko sa inyo;
            ["ansn"] = 62002006, // I Juan 2:6 — Ang nagsasabing siya'y nananahan sa kaniya ay nararapat din namang lumakad na gaya ng inilakad niya.
            ["mmba"] = 64001013, // III Juan 1:13 — Maraming mga bagay ang isusulat ko sana sa iyo, datapuwa't hindi ko ibig isulat sa iyo ng tinta at panulat:
            ["sans"] = 65001011, // Judas 1:11 — Sa aba nila! sapagka't sila'y nagsilakad sa daan ni Cain, at nagsidaluhong na walang pagpipigil sa kamalian ni Balaam dahil sa upa, at nangapahamak sa pagsalangsang ni Core.
            ["aalu"] = 66001012, // Apocalipsis 1:12 — At ako'y lumingon upang makita ang tinig na nagsasalita sa akin. At nang ako'y lumingon ay nakita ko ang pitong kandelerong ginto:
            ["asmn"] = 40005040, // Mateo 5:40 — At sa magibig na ikaw ay ipagsakdal, at kunin sa iyo ang iyong tunika, ay iwan mo rin naman sa kaniya ang iyong balabal.
            ["nmam"] = 41010019, // Marcos 10:19 — Nalalaman mo ang mga utos, Huwag kang pumatay, Huwag kang mangalunya, Huwag kang magnakaw, Huwag kang sumaksi sa di katotohanan, Huwag kang magdaya, Igalang mo ang iyong ama at ang iyong ina.
            ["apnb"] = 42006019, // Lucas 6:19 — At pinagpipilitan ng buong karamihan na siya'y mahipo; sapagka't lumalabas sa kaniya ang makapangyarihang bisa, at nagpapagaling sa lahat.
            ["sskk"] = 43001016, // Juan 1:16 — Sapagka't sa kaniyang kapuspusan ay nagsitanggap tayong lahat, at biyaya sa biyaya.
            ["mkkm"] = 44001016, // Gawa 1:16 — Mga kapatid, kinakailangang matupad ang kasulatan, na nang una ay sinalita ng Espiritu Santo, sa pamamagitan ng bibig ni David tungkol kay Judas, na siyang pumatnugot sa nagsihuli kay Jesus.
            ["akba"] = 45003014, // Roma 3:14 — Ang kanilang bibig ay puno ng panunumpa at ng kapaitan:
            ["nasd"] = 46001014, // I Corinto 1:14 — Nagpapasalamat ako sa Dios na hindi ko binautismuhan ang sinoman sa inyo, maliban si Crispo at si Gayo;
            ["skan"] = 47004018, // II Corinto 4:18 — Samantalang kami ay nagsisitingin hindi sa mga bagay na nangakikita, kundi sa mga bagay na hindi nangakikita: sapagka't ang mga bagay na nangakikita ay may katapusan; datapuwa't ang mga bagay na hindi nangakikita ay walang hanggan.
            ["aaim"] = 48002013, // Galacia 2:13 — At ang ibang mga Judio ay nangagpakunwari rin namang kasama niya: ano pa't pati si Bernabe ay nabuyo sa kanilang pagkukunwari.
            ["uisi"] = 49001017, // Efeso 1:17 — Upang ipagkaloob sa inyo ng Dios ng ating Panginoong Jesucristo, ng Ama ng kaluwalhatian, ang espiritu ng karunungan at ng pahayag sa pagkakilala sa kaniya;
            ["aaks"] = 50001014, // Filipos 1:14 — At ang karamihan sa mga kapatid sa Panginoon, na palibhasa'y may pagkakatiwala sa aking mga tanikala, ay lalong nagkaroon ng tapang upang salitaing walang takot ang salita ng Dios.
            ["nskn"] = 51001014, // Colosas 1:14 — Na siyang kinaroroonan ng ating katubusan, na siyang kapatawaran ng ating mga kasalanan:
            ["sapd"] = 52004016, // I Tesalonica 4:16 — Sapagka't ang Panginoon din ang bababang mula sa langit, na may isang sigaw, may tinig ng arkanghel, at may pakakak ng Dios: at ang nangamatay kay Cristo ay unang mangabubuhay na maguli;
            ["tapa"] = 54001015, // I Timoteo 1:15 — Tapat ang pasabi, at nararapat tanggapin ng lahat, na si Cristo Jesus ay naparito sa sanglibutan upang iligtas ang mga makasalanan; na ako ang pangulo sa mga ito;
            ["knsn"] = 55001017, // II Timoteo 1:17 — Kundi, nang siya'y nasa Roma, ay hinanap niya ako ng buong sikap, at ako'y nasumpungan niya.
            ["nhms"] = 56001014, // Tito 1:14 — Na huwag mangakinig sa mga katha ng mga Judio, at sa mga utos ng mga tao na nangagsisisinsay sa katotohanan.
            ["nhna"] = 57001016, // Filemon 1:16 — Na hindi na alipin, kundi higit sa alipin, isang kapatid na minamahal, lalong lalo na sa akin, nguni't gaano pa kaya sa iyo, na siya'y minamahal mo maging sa laman at gayon din sa Panginoon.
            ["awan"] = 58004013, // Hebreo 4:13 — At walang anomang nilalang na hindi nahahayag sa kaniyang paningin: nguni't ang lahat ng mga bagay ay hubad at hayag sa harapan ng mga mata niyaong ating pagsusulitan.
            ["hkpm"] = 59001016, // Santiago 1:16 — Huwag kayong padaya, mga minamahal kong kapatid.
            ["kiba"] = 60001013, // I Pedro 1:13 — Kaya't inyong bigkisan ang mga baywang ng inyong pagiisip, na maging mapagpigil kayo at inyong ilagak na lubos ang inyong pagasa sa biyayang dadalhin sa inyo sa pagkahayag ni Jesucristo;
            ["opkn"] = 61001015, // II Pedro 1:15 — Oo, pagsisikapan ko na sa tuwituwi na, pagkamatay ko'y inyong maalaala ang mga bagay na ito.
            ["mmwa"] = 62002007, // I Juan 2:7 — Mga minamahal, wala akong isinusulat sa inyo na anomang bagong utos, kundi ang dating utos na nasa inyo buhat nang pasimula; ang dating utos ay ang salita na inyong narinig.
            ["dikm"] = 64001014, // III Juan 1:14 — Datapuwa't inaasahan kong makita kang madali, at tayo'y magkakausap ng mukhaan. Ang kapayapaa'y sumainyo nawa. Binabati ka ng mga kaibigan. Batiin mo ang mga kaibigan sa pangalan.
            ["amip"] = 65001012, // Judas 1:12 — Ang mga ito'y pawang mga batong natatago sa inyong piging ng pagiibigan, kung sila'y nakikipagpiging sa inyo, mga pastor na walang takot na nangagpapasabsab sa kanilang sarili; mga alapaap na walang tubig, na tinatangay ng mga hangin; mga punong kahoy sa taginaw na walang bunga, na makalawang namatay, na binunot pati ugat;
            ["asgn"] = 66001013, // Apocalipsis 1:13 — At sa gitna ng mga kandelero ay may isang katulad ng isang anak ng tao, na may suot na damit hanggang sa paa, at may bigkis ang dibdib na isang pamigkis na ginto.
            ["assp"] = 40005041, // Mateo 5:41 — At sa sinomang pipilit sa iyo na ikaw ay lumakad ng isang milya, ay lumakad ka ng dalawang milya na kasama niya.
            ["apsk"] = 41010021, // Marcos 10:21 — At pagtitig sa kaniya ni Jesus, ay giniliw siya, at sinabi sa kaniya, Isang bagay ang kulang sa iyo: yumaon ka, ipagbili mo ang lahat mong tinatangkilik, at ibigay mo sa dukha, at magkakaroon ka ng kayamanan sa langit: at pumarito ka, sumunod ka sa akin.
            ["aina"] = 42006020, // Lucas 6:20 — At itiningin niya ang kaniyang mga mata sa kaniyang mga alagad, at sinabi, Mapapalad kayong mga dukha: sapagka't inyo ang kaharian ng Dios.
            ["sisp"] = 43001017, // Juan 1:17 — Sapagka't ibinigay sa pamamagitan ni Moises ang kautusan; ang biyaya at ang katotohanan ay nagsidating sa pamamagitan ni Jesu-cristo.
            ["ssis"] = 44001017, // Gawa 1:17 — Sapagka't siya'y ibinilang sa atin, at siya'y tumanggap ng kaniyang bahagi sa ministeriong ito.
            ["akmp"] = 45003015, // Roma 3:15 — Ang kanilang mga paa ay matulin sa pagbububo ng dugo;
            ["abkr"] = 46001016, // I Corinto 1:16 — At binautismuhan ko rin naman ang sangbahayan ni Estefanas: maliban sa mga ito, di ko maalaman kung may nabautismuhan akong iba pa.
            ["snnn"] = 47005001, // II Corinto 5:1 — Sapagka't nalalaman namin na kung masira ang aming bahay na tabernakulong ukol sa lupa, ay mayroong kaming isang gusaling mula sa Dios, bahay na hindi gawa ng mga kamay, walang hanggan, sa sangkalangitan.
            ["nnam"] = 48002014, // Galacia 2:14 — Nguni't nang aking makita na hindi sila nagsisilakad ng matuwid ayon sa katotohanan ng evangelio, sinabi ko kay Cefas sa harapan nilang lahat, Kung ikaw, na Judio, ay namumuhay gaya ng mga Gentil, at di gaya ng mga Judio, bakit mo pinipilit ang mga Gentil na mamuhay na gaya ng mga Judio?
            ["nkgk"] = 49001020, // Efeso 1:20 — Na kaniyang ginawa kay Cristo, nang ito'y kaniyang buhaying maguli sa mga patay, at pinaupo sa kaniyang kanan sa sangkalangitan,
            ["tnin"] = 50001015, // Filipos 1:15 — Tunay na ipinangangaral ng iba si Cristo sa kapanaghilian at sa pakikipagtalo; at ng mga iba naman sa mabuting kalooban:
            ["nsal"] = 51001015, // Colosas 1:15 — Na siya ang larawan ng Dios na di nakikita, ang panganay ng lahat ng mga nilalang;
            ["kmtn"] = 52004017, // I Tesalonica 4:17 — Kung magkagayon, tayong nangabubuhay, na nangatitira, ay aagawing kasama nila sa mga alapaap, upang salubungin ang Panginoon sa hangin: at sa ganito'y sasa Panginoon tayo magpakailan man.
            ["gmdd"] = 54001016, // I Timoteo 1:16 — Gayon ma'y dahil dito, kinahabagan ako, upang sa akin na pangulong makasalanan, ay maipahayag ni Jesucristo ang buong pagpapahinuhod niya, na halimbawa sa mga magsisisampalataya sa kaniya, sa ikabubuhay na walang hanggan.
            ["pnsn"] = 55001018, // II Timoteo 1:18 — (Pagkalooban nawa siya ng Panginoon na masumpungan niya ang kahabagan ng Panginoon sa araw na yaon); at totoong alam mo kung gaano karaming mga bagay ang ipinaglingkod niya sa Efeso.
            ["smal"] = 56001015, // Tito 1:15 — Sa malinis ang lahat ng mga bagay ay malinis: datapuwa't sa nangahawa at di nagsisisampalataya ay walang anomang malinis; kundi pati ng kanilang pagiisip at kanilang budhi ay pawang nangahawa.
            ["kana"] = 57001017, // Filemon 1:17 — Kung ako nga ay inaari mong kasama, ay tanggapin mo siyang tila ako rin.
            ["stwi"] = 58004015, // Hebreo 4:15 — Sapagka't tayo'y walang isang dakilang saserdote na hindi maaaring mahabag sa ating kahinaan, kundi isa na tinukso sa lahat ng mga paraan gaya rin naman natin gayon ma'y walang kasalanan.
            ["abmk"] = 59001017, // Santiago 1:17 — Ang bawa't mabuting kaloob at ang bawa't sakdal na kaloob ay pawang buhat sa itaas, na bumababa mula sa Ama ng mga ilaw, na walang pagbabago, ni kahit anino man ng pagiiba.
            ["ngnm"] = 60001014, // I Pedro 1:14 — Na gaya ng mga anak na matalimahin, na huwag kayong mangagasal na ayon sa inyong dating mga masasamang pita nang kayo'y na sa kawalang kaalaman:
            ["skah"] = 61001016, // II Pedro 1:16 — Sapagka't kami ay hindi nagsisunod sa mga kathang ginawang mainam, noong aming ipinakilala sa inyo ang kapangyarihan at pagparito ng ating Panginoong Jesucristo, kundi kami ay naging mga saksing nakakita ng kaniyang karangalan.
            ["mibu"] = 62002008, // I Juan 2:8 — Muli, isang bagong utos ang isinusulat ko sa inyo, bagay na tunay sa kaniya at sa inyo; sapagka't ang kadiliman ay lumilipas, at ang tunay na ilaw ay lumiliwanag na.
            ["mmna"] = 65001013, // Judas 1:13 — Mga mabangis na alon sa dagat, na pinagbubula ang kanilang sariling kahihiyan; mga bituing gala na siyang pinaglaanan ng pusikit ng kadiliman magpakailan man.
            ["aaku"] = 66001014, // Apocalipsis 1:14 — At ang kaniyang ulo at ang kaniyang buhok ay mapuputing gaya ng balahibong maputi ng tupa, gaya ng niebe; at ang kaniyang mga mata ay gaya ng ningas ng apoy;
            ["nnsi"] = 40005043, // Mateo 5:43 — Narinig ninyong sinabi, Iibigin mo ang iyong kapuwa, at kapopootan mo ang iyong kaaway:
            ["dsns"] = 41010022, // Marcos 10:22 — Datapuwa't siya'y nahapis sa sabing ito, at siya'y yumaong namamanglaw: sapagka't siya'y isang may maraming mga pag-aari.
            ["mknn"] = 42006021, // Lucas 6:21 — Mapapalad kayong nangagugutom ngayon: sapagka't kayo'y bubusugin. Mapapalad kayong nagsisitangis ngayon: sapagka't kayo'y magsisitawa.
            ["wtnk"] = 43001018, // Juan 1:18 — Walang taong nakakita kailan man sa Dios; ang bugtong na Anak, na nasa sinapupunan ng Ama, siya ang nagpakilala sa kanya.
            ["knat"] = 44001018, // Gawa 1:18 — Kumuha nga ang taong ito ng isang parang sa pamamagitan ng ganti sa kaniyang katampalasanan; at sa pagpapatihulog ng patiwarik, ay pumutok siya sa gitna, at sumambulat ang lahat ng mga laman ng kaniyang tiyan.
            ["kaka"] = 45003016, // Roma 3:16 — Kagibaan at kahirapan ang nasa kanilang mga daan;
            ["shas"] = 46001017, // I Corinto 1:17 — Sapagka't hindi ako sinugo ni Cristo upang bumautismo, kundi upang mangaral ng evangelio: hindi sa karunungan ng mga salita baka mawalan ng kabuluhan ang krus ni Cristo.
            ["stns"] = 47005002, // II Corinto 5:2 — Sapagka't tunay na sa ganito kami ay nagsisihibik, na nangagnanasang mabihisan kami ng aming tahanang mula sa langit:
            ["tmjs"] = 48002015, // Galacia 2:15 — Tayo'y mga Judio sa katutubo, at hindi mga makasalanang Gentil,
            ["sknl"] = 49001021, // Efeso 1:21 — Sa kaibaibabawan ng lahat na pamunuan, at kapamahalaan, at kapangyarihan, at pagkasakop, at sa bawa't pangalan na ipinangungusap, hindi lamang sa sanglibutang ito, kundi naman sa darating:
            ["aign"] = 50001016, // Filipos 1:16 — Ang isa'y gumagawa nito sa pagibig, palibhasa'y nalalaman na ako'y nalalagay sa pagsasanggalang ng evangelio;
            ["asus"] = 51001017, // Colosas 1:17 — At siya'y una sa lahat ng mga bagay, at ang lahat ng mga bagay ay nangabubuhay dahil sa kaniya.
            ["kmks"] = 52004018, // I Tesalonica 4:18 — Kaya't mangagaliwan kayo sa isa't isa ng mga salitang ito.
            ["nshw"] = 54001017, // I Timoteo 1:17 — Ngayon sa Haring walang hanggan, walang kamatayan, di nakikita, sa iisang Dios, ay ang kapurihan at kaluwalhatian magpakailan kailan man. Siya nawa.
            ["inak"] = 55002001, // II Timoteo 2:1 — Ikaw nga, anak ko, magpakalakas ka sa biyayang nasa kay Cristo Jesus.
            ["nmkn"] = 56002001, // Tito 2:1 — Nguni't magsalita ka ng mga bagay na nauukol sa aral na magaling:
            ["nksn"] = 57001018, // Filemon 1:18 — Nguni't kung siya'y nagkasala sa iyo ng anoman, o may utang sa iyong anoman, ay ibilang mo sa akin;
            ["mntm"] = 58004016, // Hebreo 4:16 — Magsilapit nga tayong may pagkakatiwala sa luklukan ng biyaya, upang tayo'y magsipagtamo ng awa, at mangakasumpong ng biyaya upang tumulong sa atin sa panahon ng pangangailangan.
            ["sksk"] = 59001018, // Santiago 1:18 — Sa kaniyang sariling kalooban ay kaniya tayong ipinanganak sa pamamagitan ng salita ng katotohanan, upang tayo'y maging isang uri ng mga pangunahing bunga ng kaniyang mga nilalang.
            ["nyba"] = 60001015, // I Pedro 1:15 — Nguni't yamang banal ang sa inyo'y tumawag, ay mangagpakabanal naman kayo sa lahat ng paraan ng pamumuhay;
            ["ssts"] = 61001017, // II Pedro 1:17 — Sapagka't siya'y tumanggap sa Dios Ama ng karangalan at kaluwalhatian, nang dumating sa kaniya ang isang tinig mula sa Marangal na Kaluwalhatian, Ito ang sinisinta kong Anak, na siya kong kinalulugdan:
            ["ausk"] = 62002010, // I Juan 2:10 — Ang umiibig sa kaniyang kapatid ay nananahan sa liwanag, at sa kaniya'y walang anomang kadahilanang ikatitisod.
            ["aami"] = 65001014, // Judas 1:14 — At ang mga ito naman ang hinulaan ni Enoc, na ikapito sa bilang mula kay Adam, na nagsabi, Narito, dumating ang Panginoon, na kasama ang kaniyang mga laksalaksang banal,
            ["aakm"] = 66001015, // Apocalipsis 1:15 — At ang kaniyang mga paa ay katulad ng tansong binuli, na gaya ng dinalisay sa isang lutuang-bakal; at ang kaniyang tinig ay gaya ng lagaslas ng maraming tubig.
            ["ukmm"] = 40005045, // Mateo 5:45 — Upang kayo'y maging mga anak ng inyong Ama na nasa langit: sapagka't pinasisikat niya ang kaniyang araw sa masasama at sa mabubuti, at nagpapaulan sa mga ganap at sa mga hindi ganap.
            ["anam"] = 41010024, // Marcos 10:24 — At nangagtaka ang mga alagad sa kaniyang mga salita. Datapuwa't si Jesus ay muling sumagot at nagsabi sa kanila, Mga anak, kay hirap na magsipasok sa kaharian ng Dios ang mga magsisiasa sa mga kayamanan!
            ["mkkk"] = 42006022, // Lucas 6:22 — Mapapalad kayo kung kayo'y kapootan ng mga tao, at kung kayo'y ihiwalay nila, at kayo'y alimurahin, at itakuwil ang inyong pangalan na tila masasama, dahil sa Anak ng tao.
            ["akia"] = 43001020, // Juan 1:20 — At kaniyang ipinahayag, at hindi ikinaila; at kaniyang ipinahayag, Hindi ako ang Cristo.
            ["ains"] = 44001019, // Gawa 1:19 — At ito'y nahayag sa lahat ng mga nagsisitahan sa Jerusalem; ano pa't tinawag ang parang na yaon sa kanilang wika na Aceldama, sa makatuwid baga'y, Ang parang ng Dugo.)
            ["aadn"] = 45003017, // Roma 3:17 — At ang daan ng kapayapaan ay hindi nila nakilala;
            ["sasn"] = 46001018, // I Corinto 1:18 — Sapagka't ang salita ng krus ay kamangmangan sa kanila na nangapapahamak; nguni't ito'y kapangyarihan ng Dios sa atin na nangaliligtas.
            ["nkmn"] = 47005003, // II Corinto 5:3 — Na kung mabihisan nga kami niyaon ay hindi kami mangasusumpungang hubad.
            ["nksa"] = 48002017, // Galacia 2:17 — Nguni't kung, samantalang ating pinagsisikapan na tayo'y ariing-ganap kay Cristo, ay tayo rin naman ay nangasusumpungang mga makasalanan, si Cristo baga ay ministro ng kasalanan? Huwag nawang mangyari.
            ["aaln"] = 49001022, // Efeso 1:22 — At ang lahat ng mga bagay ay pinasuko niya sa ilalim ng kaniyang mga paa, at siyang pinagkaloobang maging pangulo ng lahat ng mga bagay sa iglesia,
            ["dini"] = 50001017, // Filipos 1:17 — Datapuwa't itinatanyag ng iba si Cristo dahil sa pagkakampikampi, hindi sa pagtatapat, na ang iniisip ay dalhan ako ng kapighatian sa aking mga tanikala.
            ["asau"] = 51001018, // Colosas 1:18 — At siya ang ulo ng katawan, sa makatuwid baga'y ng iglesia; na siya ang pasimula, ang panganay sa mga patay; upang sa lahat ng mga bagay, ay magkaroon siya ng kadakilaan.
            ["dtsm"] = 52005001, // I Tesalonica 5:1 — Datapuwa't tungkol sa mga kapanahunan at mga bahagi ng panahon, mga kapatid, hindi ninyo kailangan na isulat ko pa sa inyo ang anoman.
            ["abia"] = 54001018, // I Timoteo 1:18 — Ang biling ito ay ipinagtatagubilin ko sa iyo, Timoteo na aking anak, ayon sa mga hula na nangauna tungkol sa iyo, upang sa pamamagitan ng mga ito ay makipagbaka ka ng mabuting pakikipagbaka;
            ["aamb"] = 55002002, // II Timoteo 2:2 — At ang mga bagay na iyong narinig sa akin sa gitna ng maraming saksi, ay siya mo ring ipagkatiwala sa mga taong tapat, na makapagtuturo naman sa mga iba.
            ["naml"] = 56002002, // Tito 2:2 — Na ang matatandang lalake ay maging mapagpigil, mahusay, mahinahon ang pagiisip, magagaling sa pananampalataya, sa pagibig, sa pagtitiis:
            ["okmn"] = 57001020, // Filemon 1:20 — Oo, kapatid, magkaroon nawa ako ng katuwaan sa iyo sa Panginoon: panariwain mo ang aking puso kay Cristo.
            ["sspn"] = 58011002, // Hebreo 11:2 — Sapagka't sa pamamagitan nito ang mga matanda ay sinaksihan.
            ["nnim"] = 59001019, // Santiago 1:19 — Nalalaman ninyo ito, minamahal kong mga kapatid. Nguni't magmaliksi ang bawa't tao sa pakikinig, magmakupad sa pananalita, magmakupad sa pagkagalit;
            ["snkm"] = 60001016, // I Pedro 1:16 — Sapagka't nasusulat, Kayo'y mangagpakabanal; sapagka't ako'y banal.
            ["aatn"] = 61001018, // II Pedro 1:18 — At ang tinig na ito ay aming narinig na nanggaling sa langit, nang kami ay kasama niya sa banal na bundok.
            ["nans"] = 62002011, // I Juan 2:11 — Nguni't ang napopoot sa kaniyang kapatid ay nasa kadiliman, at lumalakad sa kadiliman, at hindi niya nalalaman kung saan siya naparoroon, sapagka't ang kaniyang mga mata ay binulag ng kadiliman.
            ["uiap"] = 65001015, // Judas 1:15 — Upang isagawa ang paghuhukom sa lahat, at upang sumbatan ang lahat ng masasama sa lahat ng kanilang mga gawang masasama na kanilang ginawang may kasamaan, at sa lahat ng mga bagay na mabibigat na sinalita laban sa kaniya ng mga makasalanang masasama.
            ["askk"] = 66001016, // Apocalipsis 1:16 — At sa kaniyang kanang kamay ay may pitong bituin: at sa kaniyang bibig ay lumabas ang isang matalas na tabak na may dalawang talim: at ang kaniyang mukha ay gaya ng araw na sumisikat ng matindi.
            ["skki"] = 40005046, // Mateo 5:46 — Sapagka't kung kayo'y iibig sa nangagsisiibig lamang sa inyo, ano ang ganti na inyong kakamtin? hindi baga gayon din ang ginagawa ng mga maniningil ng buwis?
            ["mpsi"] = 41010025, // Marcos 10:25 — Magaan pa sa isang kamelyo ang dumaan sa butas ng isang karayom, kay sa isang mayaman ang pumasok sa kaharian ng Dios.
            ["dsan"] = 42006024, // Lucas 6:24 — Datapuwa't sa aba ninyong mayayaman! sapagka't tinanggap na ninyo ang inyong kaaliwan.
            ["snns"] = 43001022, // Juan 1:22 — Sinabi nga nila sa kaniya, Sino ka baga? Upang ibigay namin ang kasagutan sa nangagsugo sa amin. Ano ang sinasabi mo tungkol sa iyong sarili?
            ["smti"] = 44001021, // Gawa 1:21 — Sa mga taong ito nga na nangakisama sa atin sa buong panahon na ang Panginoong si Jesus ay pumapasok at lumalabas sa atin,
            ["wpsd"] = 45003018, // Roma 3:18 — Walang pagkatakot sa Dios sa harap ng kanilang mga mata.
            ["snik"] = 46001019, // I Corinto 1:19 — Sapagka't nasusulat, Iwawalat ko ang karunungan ng marurunong, At isasawala ko ang kabaitan ng mababait.
            ["stnk"] = 47005004, // II Corinto 5:4 — Sapagka't tunay na kaming nangasa tabernakulong ito ay nagsisihibik, na nangabibigatan; hindi sa ninanasa naming maging hubad, kundi ninanasa naming kami'y bihisan, upang ang may kamatayan ay lamunin ng buhay.
            ["kanm"] = 48002018, // Galacia 2:18 — Kung akin ngang muling itayo ang mga bagay na aking sinira, sa aking sarili ay pinatutunayan ko na ako'y suwail.
            ["akbn"] = 49002001, // Efeso 2:1 — At kayo'y binuhay niya, nang kayo'y mga patay dahil sa inyong mga pagsalangsang at mga kasalanan,
            ["angm"] = 50001018, // Filipos 1:18 — Ano nga? gayon man, sa lahat ng paraan, maging sa pagdadahilan o sa katotohanan, ay itinatanyag si Cristo; at sa ganito'y nagagalak ako, oo, at ako'y magagalak.
            ["aknn"] = 51001021, // Colosas 1:21 — At kayo, nang nakaraang panahon ay nangahihiwalay at mga kaaway sa inyong pagiisip sa inyong mga gawang masasama.
            ["skra"] = 52005002, // I Tesalonica 5:2 — Sapagka't kayo rin ang mga lubos na nangakakaalam, na ang pagdating ng kaarawan ng Panginoon ay gaya ng magnanakaw sa gabi.
            ["nima"] = 54001019, // I Timoteo 1:19 — Na ingatan mo ang pananampalataya at ang mabuting budhi; na nang ito'y itakuwil ng iba sa kanila ay nangabagbag tungkol sa pananampalataya:
            ["sknn"] = 55002004, // II Timoteo 2:4 — Sinomang kawal na nasa pagkakawal ay hindi nahalubilo sa mga bagay ng buhay na ito; upang siya'y kalugdan niyaong nagtala sa pagkakawal.
            ["ngda"] = 56002003, // Tito 2:3 — Na gayon din ang matatandang babae ay maging magalang sa kanilang kilos, hindi palabintangin ni paalipin man sa maraming alak, mga guro ng kabutihan;
            ["kssp"] = 57001021, // Filemon 1:21 — Kita'y sinulatan sa pagkakatiwala sa iyong pagtalima, palibhasa'y aking nalalaman na gagawin mo ang higit pa kay sa aking sinasabi.
            ["span"] = 58011003, // Hebreo 11:3 — Sa pananampalataya ay natatalastas natin na ang sanglibutan ay natatag sa pamamagitan ng salita ng Dios, ano pa't ang nakikita ay hindi ginawa sa mga bagay na nakikita.
            ["sagn"] = 59001020, // Santiago 1:20 — Sapagka't ang galit ng tao ay hindi gumagawa ng katuwiran ng Dios.
            ["akit"] = 60001017, // I Pedro 1:17 — At kung inyong tinatawagan, na Ama, siyang walang itinatanging tao, na humahatol ayon sa gawa ng bawa't isa, ay gugulin ninyo sa takot ang panahon ng inyong pangingibang bayan:
            ["akam"] = 61001019, // II Pedro 1:19 — At kami ay mayroong lalong panatag na salita ng hula; na mabuti ang inyong ginagawa kung ito'y inyong sinusundan, na gaya ng sa isang ilawang lumiliwanag sa isang dakong madilim, hanggang sa pagbubukang liwayway, at ang tala sa umaga ay sumilang sa inyong mga puso:
            ["kskm"] = 62002012, // I Juan 2:12 — Kayo'y sinusulatan ko, mumunti kong mga anak, sapagka't ipinatawad sa inyo ang inyong mga kasalanan dahil sa kaniyang pangalan.
            ["amim"] = 65001016, // Judas 1:16 — Ang mga ito'y mga mapagbulong, mga madaingin, na nangagsisilakad ayon sa kanilang masasamang pita (at ang kanilang bibig ay nangagsasalita ng mga kapalaluan), nangagpapakita ng galang sa mga tao dahil sa pakikinabangin.
            ["ansa"] = 66001017, // Apocalipsis 1:17 — At nang siya'y aking makita, ay nasubasob akong waring patay sa kaniyang paanan. At ipinatong niya sa akin ang kaniyang kanang kamay, na sinasabi, Huwag kang matakot; ako'y ang una at ang huli,
            ["knmn"] = 40005048, // Mateo 5:48 — Kayo nga'y mangagpakasakdal, na gaya ng inyong Ama sa kalangitan na sakdal.
            ["asnl"] = 41010026, // Marcos 10:26 — At sila'y nangagtatakang lubha, na sinasabi sa kaniya, Sino nga kaya ang makaliligtas?
            ["sanm"] = 42006025, // Lucas 6:25 — Sa aba ninyo mga busog ngayon! sapagka't kayo'y mangagugutom. Sa aba ninyong nagsisitawa ngayon! sapagka't kayo'y magsisitaghoy at magsisitangis.
            ["snaa"] = 43001023, // Juan 1:23 — Sinabi niya, Ako ang tinig ng isang humihiyaw sa ilang, Tuwirin ninyo ang daan ng Panginoon, gaya ng sinabi ng propeta Isaias.
            ["mspn"] = 44001022, // Gawa 1:22 — Magmula sa pagbautismo ni Juan, hanggang sa araw na siya'y tanggapin sa itaas mula sa atin, ay nararapat na ang isa sa mga ito'y maging saksi na kasama natin sa kaniyang pagkabuhay na maguli.
            ["nann"] = 45003019, // Roma 3:19 — Ngayon ay nalalaman natin na ang anomang sinasabi ng kautusan, yaon ay sinasabi sa nangasa ilalim ng kautusan; upang matikom ang bawa't bibig, at ang buong sanglibutan ay mapasa ilalim ng hatol ng Dios:
            ["snam"] = 46001020, // I Corinto 1:20 — Saan naroon ang marunong? saan naroon ang eskriba? saan naroon ang mapagmatuwid sa sanglibutang ito? hindi baga ginawa ng Dios na kamangmangan ang karunungan ng sanglibutan?
            ["nags"] = 47005005, // II Corinto 5:5 — Ngayon ang gumawa sa amin ng bagay ding ito ay ang Dios, na nagbigay sa amin ng patotoo ng Espiritu.
            ["sasp"] = 48002019, // Galacia 2:19 — Sapagka't ako sa pamamagitan ng kautusan ay namatay, sa kautusan, upang ako'y mabuhay sa Dios.
            ["ninn"] = 49002002, // Efeso 2:2 — Na inyong nilakaran noong una ayon sa lakad ng sanglibutang ito, ayon sa pangulo ng mga kapangyarihan ng hangin, ng espiritu na ngayon ay gumagawa sa mga anak ng pagsuway;
            ["asam"] = 50001020, // Filipos 1:20 — Ayon sa aking maningas na paghihintay at pagasa, na, sa anoma'y hindi ako mapapahiya, kundi sa buong katapangan, na gaya ng dati, gayon din naman ngayon, ay dadakilain si Cristo sa aking katawan, maging sa pamamagitan ng kabuhayan, o sa pamamagitan ng kamatayan.
            ["gmpn"] = 51001022, // Colosas 1:22 — Gayon ma'y pinakipagkasundo niya ngayon sa katawan ng kaniyang laman, sa pamamagitan ng kamatayan, upang kayo'y iharap na mga banal at walang dungis at walang kapintasan sa harapan niya:
            ["psnm"] = 52005003, // I Tesalonica 5:3 — Pagka sinasabi ng mga tao, Kapayapaan at katiwasayan, kung magkagayo'y darating sa kanila ang biglang pagkawasak, na gaya ng pagdaramdam, sa panganganak ng babaing nagdadalang-tao; at sila'y hindi mangakatatanan sa anomang paraan.
            ["nsmi"] = 54001020, // I Timoteo 1:20 — Na sa mga ito'y si Himeneo at si Alejandro; na sila'y aking ibinigay kay Satanas, upang sila'y maturuang huwag mamusong.
            ["akas"] = 55002005, // II Timoteo 2:5 — At kung ang sinoman ay makikipaglaban naman sa mga laro, ay hindi pinuputungan maliban na kung makipaglabang matuwid.
            ["ukma"] = 56002004, // Tito 2:4 — Upang kanilang maturuan ang mga babaing may kabataan na magsiibig sa kanikaniyang asawa, magsiibig sa kanilang mga anak, mangagpakahinahon,
            ["dbal"] = 57001022, // Filemon 1:22 — Datapuwa't bago ang lahat ay ipaghanda mo ako ng matutuluyan: sapagka't inaasahan kong sa pamamagitan ng inyong mga panalangin ay ipagkakaloob ako sa inyo.
            ["spsa"] = 58011004, // Hebreo 11:4 — Sa pananampalataya si Abel ay naghandog sa Dios ng lalong mabuting hain kay sa kay Cain, sa pamamagitan nito'y sinaksihan sa kaniyang siya'y matuwid, na nagpapatotoo ang Dios tungkol sa kaniyang mga kaloob: at sa pamamagitan nito patay na siya ay nagsasalita pa.
            ["skas"] = 59001023, // Santiago 1:23 — Sapagka't kung ang sinoman ay tagapakinig ng salita at hindi tagatupad, ay katulad siya ng isang tao na tinitingnan ang kaniyang talagang mukha sa salamin:
            ["nink"] = 60001018, // I Pedro 1:18 — Na inyong nalalamang kayo'y tinubos hindi ng mga bagay na nangasisira, ng pilak o ginto, mula sa inyong walang kabuluhang paraan ng pamumuhay, na ipinamana sa inyo ng inyong mga magulang;
            ["nmmi"] = 61001020, // II Pedro 1:20 — Na maalaman muna ito, na alin mang hula ng kasulatan ay hindi nagbuhat sa sariling pagpapaliwanag.
            ["kasm"] = 62002014, // I Juan 2:14 — Kayo'y aking sinulatan, mga ama, sapagka't inyong nakikilala yaong buhat pa nang pasimula ay siya na. Kayo'y aking sinulatan, mga binata, sapagka't kayo'y malalakas, at ang salita ng Dios ay nananahan sa inyo, at inyong dinaig ang masama.
            ["nkmm"] = 65001017, // Judas 1:17 — Nguni't kayo, mga minamahal, ay alalahanin ninyo ang mga salitang nang una'y sinabi ng mga apostol ng ating Panginoong Jesucristo;
            ["imna"] = 66001019, // Apocalipsis 1:19 — Isulat mo nga ang mga bagay na nakita mo, at ang mga bagay ngayon, at ang mga bagay na mangyayari sa darating;
            ["imsa"] = 40006011, // Mateo 6:11 — Ibigay mo sa amin ngayon ang aming kakanin sa araw-araw.
            ["snjk"] = 41010029, // Marcos 10:29 — Sinabi ni Jesus, Katotohanang sinasabi ko sa inyo, Walang taong nagiwan ng bahay, o mga kapatid na lalake, o mga kapatid na babae, o ina, o ama, o mga anak, o mga lupa, dahil sa akin, at dahil sa evangelio,
            ["sanp"] = 42006026, // Lucas 6:26 — Sa aba ninyo, pagka ang lahat ng mga tao ay mangagsalita ng magaling tungkol sa inyo! sapagka't sa gayon ding paraan ang ginawa ng kanilang mga magulang sa mga bulaang propeta.
            ["assb"] = 43001024, // Juan 1:24 — At sila'y sinugo buhat sa mga Fariseo.
            ["utak"] = 44001025, // Gawa 1:25 — Upang tanggapin ang katungkulan sa ministeriong ito at pagkaapostol na kinahulugan ni Judas, upang siya'y makaparoon sa kaniyang sariling kalalagyan.
            ["dnbs"] = 45003021, // Roma 3:21 — Datapuwa't ngayon bukod sa kautusan ay ipinahahayag ang isang katuwiran ng Dios, na sinasaksihan ng kautusan at ng mga propeta;
            ["sysk"] = 46001021, // I Corinto 1:21 — Sapagka't yamang sa karunungan ng Dios ay hindi nakilala ng sanglibutan ang Dios sa pamamagitan ng kaniyang karunungan, ay kinalugdan ng Dios na iligtas ang mga nagsisipanampalataya sa pamamagitan ng kamangmangan ng pangangaral.
            ["knkl"] = 47005006, // II Corinto 5:6 — Kaya nga kami'y laging malakas ang loob, at nalalaman namin na, samantalang kami ay nangasa tahanan sa katawan, ay wala kami sa harapan ng Panginoon.
            ["hknn"] = 48002021, // Galacia 2:21 — Hindi ko niwawalan ng halaga ang biyaya ng Dios: sapagka't kung sa pamamagitan ng kautusan ay ang katuwiran, kung gayo'y si Cristo ay namatay ng walang kabuluhan.
            ["sgnm"] = 49002003, // Efeso 2:3 — Sa gitna ng mga yaon, tayo rin naman, ng ibang panahon ay nangabubuhay sa mga kahalayan ng ating laman, na ating ginagawa ang mga pita ng laman at ng pagiisip, at tayo noo'y katutubong mga anak ng kagalitan, gaya naman ng mga iba:
            ["ssga"] = 50001021, // Filipos 1:21 — Sapagka't sa ganang akin ang mabuhay ay si Cristo, at ang mamatay ay pakinabang.
            ["ktnk"] = 51001023, // Colosas 1:23 — Kung tunay na kayo'y mamamalagi sa pananampalataya, na nababaon at matitibay, at di makilos sa pagasa sa evangelio na inyong narinig, na ipinangaral sa lahat ng mga nilalang sa silong ng langit; na dito akong si Pablo ay ginawang ministro.
            ["nkmk"] = 52005004, // I Tesalonica 5:4 — Nguni't kayo, mga kapatid, ay wala sa kadiliman, upang sa araw na yaon ay masubukan kayong gaya ng magnanakaw:
            ["amha"] = 54002002, // I Timoteo 2:2 — Ang mga hari at ang lahat ng nangasa mataas na kalagayan; upang tayo'y mangabuhay na tahimik at payapa sa buong kabanalan at kahusayan.
            ["amnn"] = 55002006, // II Timoteo 2:6 — Ang magsasaka na nagpapagal ay siyang kailangang unang makabahagi sa mga bunga.
            ["mmms"] = 56002005, // Tito 2:5 — Mangagpakahinahon, mangagpakalinis, mangagpakasipag sa bahay, magagandang-loob, pasakop sa kanikaniyang asawa, upang huwag lapastanganin ang salita ng Dios:
            ["abna"] = 57001025, // Filemon 1:25 — Ang biyaya ng ating Panginoong Jesucristo ay sumainyo nawang espiritu. Siya nawa.
            ["spse"] = 58011005, // Hebreo 11:5 — Sa pananampalataya si Enoc ay inilipat upang huwag niyang makita ang kamatayan; at hindi siya nasumpungan, sapagka't siya'y inilipat ng Dios: sapagka't bago siya inilipat ay pinatotohanan sa kaniyang siya'y naging kalugodlugod sa Dios:
            ["kasa"] = 59001026, // Santiago 1:26 — Kung ang sinoman ay nagiisip na siya'y relihioso samantalang hindi pinipigil ang kaniyang dila, kundi dinadaya ang kaniyang puso, ang relihion ng taong ito ay walang kabuluhan.
            ["knmd"] = 60001019, // I Pedro 1:19 — Kundi ng mahalagang dugo, gaya ng sa korderong walang kapintasan at walang dungis, sa makatuwid baga'y ang dugo ni Cristo:
            ["shsk"] = 61001021, // II Pedro 1:21 — Sapagka't hindi sa kalooban ng tao dumating ang hula kailanman: kundi ang mga tao ay nagsalita buhat sa Dios, na nangaudyokan ng Espiritu Santo.
            ["aasa"] = 62002017, // I Juan 2:17 — At ang sanglibutan ay lumilipas, at ang masamang pita niyaon; datapuwa't ang gumagawa ng kalooban ng Dios ay nananahan magpakailan man.
            ["kpss"] = 65001018, // Judas 1:18 — Kung paanong sinabi sa inyo, Magkakaroon ng mga manunuya sa huling panahon, na magsisilakad ayon sa kanikanilang masasamang pita.
            ["ahnp"] = 66001020, // Apocalipsis 1:20 — Ang hiwaga ng pitong bituin na iyong nakita sa aking kanang kamay, at ng pitong kandelerong ginto. Ang pitong bituin ay ang mga anghel ng pitong iglesia: at ang pitong kandelero ay ang pitong iglesia.
            ["skin"] = 40006014, // Mateo 6:14 — Sapagka't kung ipatawad ninyo sa mga tao ang kanilang mga kasalanan, ay patatawarin naman kayo ng inyong Ama sa kalangitan.
            ["nhst"] = 41010030, // Marcos 10:30 — Na hindi siya tatanggap ng tigisang daan ngayon sa panahong ito, ng mga bahay, at mga kapatid na lalake, at mga kapatid na babae, at mga ina, at mga anak, at mga lupa, kalakip ng mga paguusig; at sa sanglibutang darating ay ng walang hanggang buhay.
            ["pnas"] = 42006028, // Lucas 6:28 — Pagpalain ninyo ang sa inyo'y sumusumpa at ipanalangin ninyo ang sa inyo'y lumalait.
            ["ssnj"] = 43001026, // Juan 1:26 — Sila'y sinagot ni Juan, na nagsasabi, Ako'y bumabautismo sa tubig: datapuwa't sa gitna ninyo'y may isang nakatayo na hindi ninyo nakikilala,
            ["andn"] = 44002001, // Gawa 2:1 — At nang dumating nga ang araw ng Pentecostes, silang lahat ay nangagkakatipon sa isang dako.
            ["smba"] = 45003022, // Roma 3:22 — Sa makatuwid baga'y ang katuwiran ng Dios sa pamamagitan ng pananampalataya kay Jesucristo sa lahat ng mga nagsisisampalataya; sapagka't walang pagkakaiba;
            ["amjn"] = 46001022, // I Corinto 1:22 — Ang mga Judio nga ay nagsisihingi ng mga tanda, at ang mga Griego ay nagsisihanap ng karunungan:
            ["nmal"] = 47005008, // II Corinto 5:8 — Na malakas ang loob namin, ang sabi ko, at ibig pa nga namin ang mawala sa katawan, at mapasa tahanan na kasama ng Panginoon.
            ["skap"] = 48005001, // Galacia 5:1 — Sa kalayaan ay pinalaya tayo ni Cristo: magsitibay nga kayo, at huwag na kayong pasakop na muli sa pamatok ng pagkaalipin.
            ["nadp"] = 49002004, // Efeso 2:4 — Nguni't ang Dios, palibhasa'y mayaman sa awa, dahil sa kaniyang malaking pagibig na kaniyang iniibig sa atin,
            ["gmam"] = 50001024, // Filipos 1:24 — Gayon ma'y ang manatili sa laman ay siyang lalong kinakailangan dahil sa inyo.
            ["nnas"] = 51001024, // Colosas 1:24 — Ngayo'y nagagalak ako sa aking mga hirap dahil sa inyo, at aking pinupunan sa akin ang kakulangan ng mga hirap ni Cristo sa aking laman dahil sa kaniyang katawan, na siyang iglesia;
            ["skla"] = 52005005, // I Tesalonica 5:5 — Sapagka't kayong lahat ay pawang mga anak ng kaliwanagan, at mga anak ng araw: tayo'y hindi ng gabi, ni ng kadiliman man;
            ["iman"] = 54002003, // I Timoteo 2:3 — Ito'y mabuti at nakalulugod sa paningin ng Dios na ating Tagapagligtas;
            ["imas"] = 55002007, // II Timoteo 2:7 — Isipin mo ang sinasabi ko; sapagka't bibigyan ka ng Panginoon ng pagkaunawa sa lahat ng mga bagay.
            ["imrn"] = 56002006, // Tito 2:6 — Iaral mo rin naman sa mga bagong tao na sila'y mangagpakahinahon ng pagiisip:
            ["spsn"] = 58011007, // Hebreo 11:7 — Sa pananampalataya si Noe, nang paunawaan ng Dios tungkol sa mga bagay na hindi pa nakikita, dala ng banal na takot, ay naghanda ng isang daong sa ikaliligtas ng kaniyang sangbahayan; na sa pamamagitan nito ay hinatulan niya ang sanglibutan, at naging tagapagmana ng katuwiran na ayon sa pananampalataya.
            ["mkky"] = 59002001, // Santiago 2:1 — Mga kapatid ko, yayamang mayroon kayong pananampalataya ng ating Panginoong Jesucristo, na Panginoon ng kaluwalhatian ay huwag magtatangi sa mga tao.
            ["nnnn"] = 60001020, // I Pedro 1:20 — Na nakilala nga nang una bago itinatag ang sanglibutan, nguni't inihayag sa mga huling panahon dahil sa inyo,
            ["nmnd"] = 61002001, // II Pedro 2:1 — Nguni't may nagsilitaw din naman sa bayan na mga bulaang propeta, na gaya naman sa inyo'y magkakaroon ng mga bulaang guro, na mangagpapasok sa lihim ng mga makakapahamak na mga hiduwang pananampalataya, na itatatuwa pati ang Panginoon na bumili sa kanila, na mangagtataglay sa kanilang sarili ng madaling pagkapahamak.
            ["mmai"] = 62002018, // I Juan 2:18 — Mumunting mga anak, ito ang huling oras: at gaya ng inyong narinig na darating ang anticristo, kahit ngayon ay lumitaw ang marami nang anticristo; kaya nga nalalaman natin na ito na ang huling oras.
            ["amia"] = 65001019, // Judas 1:19 — Ang mga ito ang nagsisigawa ng paghihiwalay, malalayaw, na walang taglay na Espiritu.
            ["asan"] = 66003001, // Apocalipsis 3:1 — At sa anghel ng iglesia sa Sardis ay isulat mo: Ang mga bagay na ito ay sinasabi ng may pitong Espiritu ng Dios, at may pitong bituin: Nalalaman ko ang iyong mga gawa, na ikaw ay may pangalang ikaw ay nabubuhay, at ikaw ay patay.
            ["dkma"] = 40006023, // Mateo 6:23 — Datapuwa't kung masama ang iyong mata, ang buong katawan mo'y mapupuspos ng kadiliman. Kaya't kung ang ilaw na sumasa iyo ay kadiliman, gaano kaya kalaki ang kadiliman!
            ["dmna"] = 41010031, // Marcos 10:31 — Datapuwa't maraming nangauuna ay mangahuhuli; at nangahuhuli na mangauuna.
            ["sssi"] = 42006029, // Lucas 6:29 — Sa sumampal sa iyo sa isang pisngi, iharap mo naman ang kabila; at ang sa iyo'y magalis ng iyong balabal, huwag mong itanggi pati ng iyong tunika.
            ["snap"] = 43001027, // Juan 1:27 — Siya nga ang pumaparitong sumusunod sa akin, na sa kaniya'y hindi ako karapatdapat magkalag ng panali ng kaniyang pangyapak.
            ["abdm"] = 44002002, // Gawa 2:2 — At biglang dumating mula sa langit ang isang ugong na gaya ng sa isang humahagibis na hanging malakas, at pinuno ang buong bahay na kanilang kinauupuan.
            ["pign"] = 45003024, // Roma 3:24 — Palibhasa'y inaring-ganap na walang bayad ng kaniyang biyaya sa pamamagitan ng pagtubos na nasa kay Cristo Jesus:
            ["daai"] = 46001023, // I Corinto 1:23 — Datapuwa't ang aming ipinangangaral ay ang Cristo na napako sa krus, na sa mga Judio ay katitisuran, at sa mga Gentil ay kamangmangan;
            ["stla"] = 47005010, // II Corinto 5:10 — Sapagka't tayong lahat ay kinakailangang mahayag sa harapan ng hukuman ni Cristo; upang tumanggap ang bawa't isa ng mga bagay na ginawa sa pamamagitan ng katawan, ayon sa ginawa niya, maging mabuti o masama.
            ["nasp"] = 48005002, // Galacia 5:2 — Narito, akong si Pablo ay nagsasabi sa inyo, na, kung inyong tinatanggap ang pagtutuli, ay wala kayong mapapakinabang na anoman kay Cristo.
            ["atik"] = 49002006, // Efeso 2:6 — At tayo'y ibinangong kalakip niya, at pinaupong kasama niya sa sangkalangitan, kay Cristo Jesus:
            ["aspi"] = 50001025, // Filipos 1:25 — At sa pagkakatiwalang ito, ay aking nalalaman na ako'y mananatili, oo, at mananatili ako na kasama ninyong lahat, sa ikasusulong ninyo at ikagagalak sa pananampalataya;
            ["nagm"] = 51001025, // Colosas 1:25 — Na ako'y ginawang ministro nito, ayon sa pamamahala na mula sa Dios na ibinigay sa akin para sa inyo upang maipahayag ang salita ng Dios,
            ["hntm"] = 52005006, // I Tesalonica 5:6 — Huwag nga tayong mangatulog, gaya ng mga iba, kundi tayo'y mangagpuyat at mangagpigil.
            ["niak"] = 54002006, // I Timoteo 2:6 — Na ibinigay ang kaniyang sarili na pangtubos sa lahat; na pagpapatotoong mahahayag sa sariling kapanahunan;
            ["amsj"] = 55002008, // II Timoteo 2:8 — Alalahanin mo si Jesucristo na muling nabuhay sa mga patay, sa binhi ni David, ayon sa aking evangelio:
            ["pnmn"] = 56002008, // Tito 2:8 — Pangungusap na magaling, na di mahahatulan; upang sila na nasa kabilang panig ay mahiya, nang walang anomang masamang masabi tungkol sa atin.
            ["sina"] = 58011010, // Hebreo 11:10 — Sapagka't inaasahan niya ang bayang may mga kinasasaligan, na ang nagtayo at gumawa ay ang Dios.
            ["skmp"] = 59002002, // Santiago 2:2 — Sapagka't kung may pumapasok sa inyong sinagoga ang isang tao na may singsing na ginto, at may magandang kasuotan, at may pumapasok namang isang dukha na may damit na hamak;
            ["nspn"] = 60001021, // I Pedro 1:21 — Na sa pamamagitan niya ay nanganampalataya kayo sa Dios, na sa kaniya'y bumuhay na maguli sa mga patay, at sa kaniya'y nagbigay ng kaluwalhatian; upang ang inyong pananampalataya at pagasa ay mapasa Dios.
            ["amms"] = 61002002, // II Pedro 2:2 — At maraming magsisisunod sa kanilang mga gawang mahahalay; na dahil sa kanila ay pagsasalitaan ng masama ang daan ng katotohanan.
            ["hkks"] = 62002021, // I Juan 2:21 — Hindi ko kayo sinulatan ng dahil sa hindi ninyo nalalaman ang katotohanan, kundi dahil sa inyong nalalaman, at sapagka't alin mang kasinungalingan ay hindi sa katotohanan.
            ["aain"] = 65001022, // Judas 1:22 — At ang ibang nagaalinlangan ay inyong kahabagan;
            ["mkap"] = 66003002, // Apocalipsis 3:2 — Magpuyat ka, at pagtibayin mo ang mga bagay na natitira, na malapit ng mamatay: sapagka't wala akong nasumpungang iyong mga gawang sakdal sa harapan ng aking Dios.
            ["shms"] = 40006024, // Mateo 6:24 — Sinoma'y hindi makapaglilingkod sa dalawang panginoon: sapagka't kapopootan niya ang isa, at iibigin ang ikalawa: o kaya'y magtatapat siya sa isa, at pawawalang halaga ang ikalawa. Hindi kayo makapaglilingkod sa Dios at sa mga kayamanan.
            ["aska"] = 41010034, // Marcos 10:34 — At siya'y kanilang aalimurahin, at siya'y luluraan, at siya'y hahampasin, at siya'y papatayin; at pagkaraan ng tatlong araw ay magbabangon siyang muli.
            ["akkm"] = 42006032, // Lucas 6:32 — At kung kayo'y magsiibig sa mga nagsisiibig sa inyo, anong pasasalamat ang inyong kakamtin? sapagka't ang mga makasalanan man ay nagsisiibig sa nagsisiibig sa kanila.
            ["nkan"] = 43001029, // Juan 1:29 — Nang kinabukasan ay nakita ni Juan si Jesus na lumalapit sa kaniya, at sinabi, Narito, ang Cordero ng Dios, na nagaalis ng kasalanan ng sanglibutan!
            ["asla"] = 44002004, // Gawa 2:4 — At silang lahat ay nangapuspos ng Espiritu Santo, at nangagpasimulang magsalita ng iba't ibang wika, ayon sa ipinagkakaloob ng Espiritu na kanilang salitain.
            ["nsin"] = 45003025, // Roma 3:25 — Na siyang inilagay ng Dios na maging pangpalubagloob, sa pamamagitan ng pananampalataya, sa kaniyang dugo, upang maipakilala ang kaniyang katuwiran dahil sa hindi pagpansin sa mga kasalanan na nagawa nang nagdaang panahon sa pagpapahinuhod ng Dios;
            ["uwln"] = 46001029, // I Corinto 1:29 — Upang walang laman na magmapuri sa harapan ng Dios.
            ["hnim"] = 47005012, // II Corinto 5:12 — Hindi namin ipinagkakapuring muli ang aming sarili sa inyo, kundi binibigyan namin kayo ng dahilan na ikaluluwalhati ninyo dahil sa amin, upang kayo'y mangagkaroon ng maisasagot sa mga nagpapaluwalhati sa anyo, at hindi sa puso.
            ["opkm"] = 48005003, // Galacia 5:3 — Oo, pinatotohanan kong muli sa bawa't taong tumatanggap ng pagtutuli, na siya'y may kautangang tumupad ng buong kautusan.
                                 // 400 additional aliases for SCommands (Ang Dating Biblia, New Testament).
                                 // Alias = first letters of the first four whitespace-separated words of the verse.
                                 // Numeric value = BookId * 1,000,000 + Chapter * 1,000 + Verse.
                                 // All keys are four letters, unique, non-existing, and do not start with b/c/v/q/r/z/x/y/f.

            // Add these entries inside the existing SCommands dictionary:
            ["aanl"] = 40001001, // Mateo 1:1 — Ang aklat ng lahi ni Jesucristo, na anak ni David, na anak ni Abraham.
            ["asss"] = 40016016, // Mateo 16:16 — At sumagot si Simon Pedro at sinabi, Ikaw ang Cristo, Ang anak ng Dios na buhay.
            ["apne"] = 41001001, // Marcos 1:1 — Ang pasimula ng evangelio ni Jesucristo, ang Anak ng Dios.
            ["knap"] = 45010017, // Roma 10:17 — Kaya nga ang paniniwala'y nanggagaling sa pakikinig, at ang pakikinig ay sa pamamagitan ng salita ni Cristo.
            ["hbnn"] = 46003016, // I Corinto 3:16 — Hindi baga ninyo nalalaman na kayo'y templo ng Dios, at ang Espiritu ng Dios ay nananahan sa inyo?
            ["ohbn"] = 46006019, // I Corinto 6:19 — O hindi baga ninyo nalalaman na ang inyong katawan ay templo ng Espiritu Santo na nasa inyo, na tinanggap ninyo sa Dios? at hindi kayo sa inyong sarili;
            //ibig; nguni't ang pinakadakila sa mga ito ay ang pagibig.
            ["ntna"] = 47004007, // II Corinto 4:7 — Nguni't taglay namin ang kayamanang ito sa mga sisidlang-lupa, upang ang dakilang kalakhan ng kapangyarihan ay maging mula sa Dios, at huwag mula sa aming sarili;
            ["mnss"] = 51003016, // Colosas 3:16 — Manahanan nawang sagana sa inyo ang salita ni Cristo; ayon sa buong karunungan, kayo'y mangagturuan at mangagpaalalahanan sa isa't isa sa pamamagitan ng mga salmo at ng mga himno at mga awit na ukol sa espiritu, na magsiawit kayong may biyaya sa inyong puso sa Dios.
            ["nnmm"] = 54001007, // I Timoteo 1:7 — Na nagsisipagnasang maging mga guro ng kautusan, bagaman di nila natatalastas kahit ang kanilang sinasabi, kahit ang kanilang buong tiwalang pinatutunayan.
            ["kynt"] = 58012001, // Hebreo 12:1 — Kaya't yamang nakukubkob tayo ng makapal na bilang ng mga saksi, itabi namang walang liwag ang bawa't pasan, at ang pagkakasalang pumipigil sa atin, at ating takbuhing may pagtitiis ang takbuhing inilagay sa harapan natin,
            ["dmtk"] = 59001022, // Santiago 1:22 — Datapuwa't maging tagatupad kayo ng salita, at huwag tagapakinig lamang, na inyong dinadaya ang inyong sarili.
            ["nana"] = 40001002, // Mateo 1:2 — Naging anak ni Abraham si Isaac; at naging anak ni Isaac si Jacob; at naging anak ni Jacob si Juda at ang kaniyang mga kapatid;
            ["anan"] = 40001003, // Mateo 1:3 — At naging anak ni Juda kay Tamar si Fares at si Zara; at naging anak ni Fares si Esrom; at naging anak ni Esrom si Aram;
            ["apnk"] = 40001018, // Mateo 1:18 — Ang pagkapanganak nga kay Jesucristo ay ganito: Nang si Maria na kaniyang ina ay magaasawa kay Jose, bago sila magsama ay nasumpungang siya'y nagdadalang-tao sa pamamagitan ng Espiritu Santo.
            ["dspn"] = 40001020, // Mateo 1:20 — Datapuwa't samantalang pinagiisip niya ito, narito, ang isang anghel ng Panginoon ay napakita sa kaniya sa panaginip, na nagsasabi: Jose, anak ni David, huwag kang mangamba sa pagtanggap kay Maria na iyong asawa: sapagka't ang kaniyang dinadalang-tao ay sa Espiritu Santo.
            ["nadm"] = 40001023, // Mateo 1:23 — Narito, ang dalaga'y magdadalang-tao at manganganak ng isang lalake, At ang pangalang itatawag nila sa kaniya ay Emmanuel; na kung liliwanagin, ay sumasa atin ang Dios.
            ["ansj"] = 40001024, // Mateo 1:24 — At nagbangon si Jose sa kaniyang pagkakatulog, at ginawa niya ang ayon sa ipinagutos sa kaniya ng anghel ng Panginoon, at tinanggap ang kaniyang asawa;
            ["ahns"] = 40001025, // Mateo 1:25 — At hindi nakilala siya hanggang sa maipanganak ang isang lalake: at tinawag niya ang kaniyang pangalang JESUS.
            ["snai"] = 40002002, // Mateo 2:2 — Saan naroon ang ipinanganak na hari ng mga Judio? sapagka't aming nakita ang kaniyang bituin sa silanganan, at naparito kami upang siya'y sambahin.
            ["nmin"] = 40002003, // Mateo 2:3 — Nang marinig ito ng haring si Herodes, ay nagulumihanan siya, at pati ng buong Jerusalem.
            ["apsl"] = 40002004, // Mateo 2:4 — At pagkatipon sa lahat ng mga pangulong saserdote at mga eskriba ng bayan, ay siniyasat niya sa kanila kung saan kaya ipanganganak ang Cristo.
            ["aibn"] = 40002006, // Mateo 2:6 — At ikaw Bet-lehem, na lupa ng Juda, Sa anomang paraan ay hindi ikaw ang pinakamaliit sa mga pangulong bayan ng Juda: Sapagka't mula sa iyo'y lalabas ang isang gobernador, Na siyang magiging pastor ng aking bayang Israel.
            ["nmtn"] = 40002007, // Mateo 2:7 — Nang magkagayo'y tinawag ni Herodes ng lihim ang mga Pantas na lalake, at kaniyang siniyasat ng buong ingat sa kanila ang panahong isinilang ng bituin.
            ["asps"] = 40002009, // Mateo 2:9 — At sila, pagkarinig sa hari ay nagsiyaon ng kanilang lakad; at narito, ang bituing kanilang nakita sa silanganan, ay nanguna sa kanila hanggang sa sumapit at tumigil sa tapat ng kinaroroonan ng sanggol.
            ["anss"] = 40002011, // Mateo 2:11 — At nagsipasok sila sa bahay, at nangakita nila ang sanggol na kasama ng kaniyang inang si Maria; at nangagpatirapa sila at nangagsisamba sa kaniya; at pagkabukas nila ng kanilang mga kayamanan ay inihandog nila sa kaniya ang mga alay, na ginto at kamangyan at mira.
            ["apps"] = 40002012, // Mateo 2:12 — At palibhasa'y pinagsabihan sila ng Dios sa panaginip na huwag silang mangagbalik kay Herodes, ay nangagsiuwi sila sa kanilang sariling lupain sa ibang daan.
            ["adhs"] = 40002015, // Mateo 2:15 — At dumoon hanggang sa pagkamatay ni Herodes: upang maganap ang sinabi ng Panginoon sa pamamagitan ng propeta, na nagsasabi, Mula sa Egipto ay tinawag ko ang aking anak.
            ["nmnm"] = 40002016, // Mateo 2:16 — Nang magkagayon, nang mapansin ni Herodes na siya'y pinaglaruan ng mga Pantas na lalake, ay nagalit na mainam, at nagutos, at ipinapatay ang lahat ng mga sanggol na lalake na nangasa Bet-lehem, at sa buong palibotlibot noon, mula sa gulang na dalawang taon hanggang sa pababa, alinsunod sa panahon ng kaniyang maingat na pagkasiyasat sa mga Pantas na lalake.
            ["nmna"] = 40002017, // Mateo 2:17 — Nang magkagayo'y naganap ang sinalita sa pamamagitan ng propeta Jeremias, na nagsasabi,
            ["itan"] = 40002018, // Mateo 2:18 — Isang tinig ay narinig sa Rama, Pananangis at kalagimlagim na iyak, Tinatangisan ni Raquel ang kaniyang mga anak; At ayaw na siyang maaliw, sapagka't sila'y wala na.
            ["npnh"] = 40002019, // Mateo 2:19 — Nguni't pagkamatay ni Herodes, narito, ang isang anghel ng Panginoon ay napakita sa panaginip kay Jose sa Egipto, na nagsasabi,
            ["mkad"] = 40002020, // Mateo 2:20 — Magbangon ka at dalhin mo ang sanggol at ang kaniyang ina, at pumatungo ka sa lupain ng Israel: sapagka't nangamatay na ang nangagmimithi sa buhay ng sanggol.
            ["dnmn"] = 40002022, // Mateo 2:22 — Datapuwa't nang mabalitaan niya na si Arquelao ang naghahari sa Judea na kahalili ng kaniyang amang si Herodes, ay natakot siyang pumatungo roon; at palibhasa'y pinagsabihan ng Dios sa panaginip, ay napatungo siya sa mga sakop ng Galilea,
            ["asda"] = 40002023, // Mateo 2:23 — At siya'y dumating at tumahan sa isang bayang tinatawag na Nazaret; upang maganap ang mga sinalita ng mga propeta, na siya'y tatawaging Nazareno.
            ["mksm"] = 40003002, // Mateo 3:2 — Mangagsisi kayo; sapagka't malapit na ang kaharian ng langit.
            ["siys"] = 40003003, // Mateo 3:3 — Sapagka't ito yaong sinalita sa pamamagitan ng propeta Isaias, na nagsasabi, Ang tinig ng isang sumisigaw sa ilang, Ihanda ninyo ang daan ng Panginoon, Tuwirin ninyo ang kaniyang mga landas.
            ["sjna"] = 40003004, // Mateo 3:4 — Si Juan nga ay nananamit ng balahibo ng kamelyo, at may isang pamigkis na katad sa palibot ng kaniyang baywang; at ang kaniyang pagkain ay mga balang at pulot-pukyutan.
            ["askb"] = 40003006, // Mateo 3:6 — At sila'y kaniyang binabautismuhan sa ilog ng Jordan, na ipinahahayag nila ang kanilang mga kasalanan.
            ["anpn"] = 40003010, // Mateo 3:10 — At ngayon pa'y nakalagay na ang palakol sa ugat ng mga punong kahoy: ang bawa't punong kahoy nga na hindi nagbubungang mabuti ay pinuputol at inihahagis sa apoy.
            ["skab"] = 40003011, // Mateo 3:11 — Sa katotohanan ay binabautismuhan ko kayo sa tubig sa pagsisisi: datapuwa't ang dumarating sa hulihan ko ay lalong makapangyarihan kay sa akin, na hindi ako karapatdapat magdala ng kaniyang pangyapak: siya ang sa inyo'y magbabautismo sa Espiritu Santo at apoy:
            ["nkka"] = 40003012, // Mateo 3:12 — Nasa kaniyang kamay ang kaniyang kalaykay, at lilinisin niyang lubos ang kaniyang giikan; at titipunin niya ang kaniyang trigo sa bangan, datapuwa't ang dayami ay susunugin sa apoy na hindi mapapatay.
            ["diss"] = 40003014, // Mateo 3:14 — Datapuwa't ibig siyang sansalain ni Juan, na nagsasabi, Kinakailangan ko na ako'y iyong bautismuhan, at ikaw ang naparirito sa akin?
            ["npnj"] = 40003015, // Mateo 3:15 — Nguni't pagsagot ni Jesus ay sinabi sa kaniya, Payagan mo ngayon: sapagka't ganyan ang nararapat sa atin, ang pagganap ng buong katuwiran. Nang magkagayo'y pinayagan niya siya.
            ["anms"] = 40003016, // Mateo 3:16 — At nang mabautismuhan si Jesus, pagdaka'y umahon sa tubig: at narito, nangabuksan sa kaniya ang mga langit, at nakita niya ang Espiritu ng Dios na bumababang tulad sa isang kalapati, at lumalapag sa kaniya;
            ["anai"] = 40003017, // Mateo 3:17 — At narito, ang isang tinig na mula sa mga langit, na nagsasabi, Ito ang sinisinta kong Anak, na siya kong lubos na kinalulugdan.
            ["ansm"] = 40004002, // Mateo 4:2 — At nang siya'y makapagayunong apat na pung araw at apat na pung gabi, sa wakas ay nagutom siya.
            ["aama"] = 40004003, // Mateo 4:3 — At ang manunukso ay dumating at nagsabi sa kaniya, Kung ikaw ang Anak ng Dios, ay ipagutos mo na ang mga batong ito ay maging mga tinapay.
            ["dssa"] = 40004004, // Mateo 4:4 — Datapuwa't siya'y sumagot, at sinabi, Nasusulat, Hindi sa tinapay lamang mabubuhay ang tao, kundi sa bawa't salitang lumalabas sa bibig ng Dios.
            ["nmds"] = 40004005, // Mateo 4:5 — Nang magkagayo'y dinala siya ng diablo sa bayang banal; at inilagay siya sa taluktok ng templo,
            ["mdsn"] = 40004008, // Mateo 4:8 — Muling dinala siya ng diablo sa isang bundok na lubhang mataas, at ipinamalas sa kaniya ang lahat ng mga kaharian sa sanglibutan, at ang kaluwalhatian nila;
            ["nmss"] = 40004010, // Mateo 4:10 — Nang magkagayo'y sinabi sa kaniya ni Jesus, Humayo ka, Satanas: sapagka't nasusulat, Sa Panginoon mong Dios sasamba ka, at siya lamang ang iyong paglilingkuran.
            ["nmis"] = 40004011, // Mateo 4:11 — Nang magkagayo'y iniwan siya ng diablo; at narito, nagsidating ang mga anghel at siya'y pinaglingkuran.
            ["nmnn"] = 40004012, // Mateo 4:12 — Nang marinig nga niya na si Juan ay dinakip, ay umuwi siya sa Galilea;
            ["apsn"] = 40004013, // Mateo 4:13 — At pagkaiwan sa Nazaret, ay naparoon siya at tumahan sa Capernaum, na nasa tabi ng dagat, sa mga hangganan ng Zabulon at Neftali:
            ["umas"] = 40004014, // Mateo 4:14 — Upang maganap ang sinalita sa pamamagitan ng propeta Isaias na nagsasabi,
            ["alnz"] = 40004015, // Mateo 4:15 — Ang lupa ni Zabulon at ang lupa ni Neftali, Sa gawing dagat, sa dako pa roon ng Jordan, Galilea ng mga Gentil,
            ["abns"] = 40004016, // Mateo 4:16 — Ang bayang nalulugmok sa kadiliman, ay Nakakita ng dakilang ilaw, At sa nangalulugmok sa pook at lilim ng kamatayan, ay Lumiwanag sa kanila ang ilaw.
            ["mnan"] = 40004017, // Mateo 4:17 — Mula noon ay nagpasimulang mangaral si Jesus, at magsabi, Mangagsisi kayo; sapagka't malapit na ang kaharian ng langit.
            ["apin"] = 40004020, // Mateo 4:20 — At pagdaka'y iniwan nila ang mga lambat, at nagsisunod sa kaniya.
            ["apsd"] = 40004021, // Mateo 4:21 — At paglakad sa dako roon ay nakita niya ang dalawa pang magkapatid, si Santiago na anak ni Zebedeo, at ang kaniyang kapatid na si Juan, sa daong na kasama si Zebedeo na kanilang ama, na nagsisipaghayuma ng kanilang mga lambat; at sila'y kaniyang tinawag.
            ["annj"] = 40004023, // Mateo 4:23 — At nilibot ni Jesus ang buong Galilea, na nagtuturo sa mga sinagoga nila, at ipinangangaral ang evangelio ng kaharian, at nagpapagaling ng lahat ng sarisaring sakit at ng lahat ng karamdaman na nasa mga tao.
            ["alap"] = 40004024, // Mateo 4:24 — At lumaganap ang pagkabantog niya sa buong Siria: at kanilang dinadala sa kaniya ang lahat ng mga may karamdaman, at ang mga pinipighati ng sarisaring sakit at pahirap, at ang mga inaalihan ng mga demonio, at ang mga himatayin, at ang mga lumpo: at sila'y pinagagaling niya.
            ["assn"] = 40004025, // Mateo 4:25 — At sinundan siya ng lubhang karamihang tao mula sa Galilea at Decapolis at Jerusalem at Judea at mula sa dakong ibayo ng Jordan.
            ["kass"] = 40005019, // Mateo 5:19 — Kaya't ang sinomang sumuway sa isa sa kaliitliitang mga utos na ito, at ituro ang gayon sa mga tao, ay tatawaging kaliitliitan sa kaharian ng langit: datapuwa't ang sinomang gumanap at ituro, ito'y tatawaging dakila sa kaharian ng langit.
            ["ssks"] = 40005020, // Mateo 5:20 — Sapagka't sinasabi ko sa inyo, na kung hindi hihigit ang inyong katuwiran sa katuwiran ng mga eskriba at mga Fariseo, sa anomang paraan ay hindi kayo magsisipasok sa kaharian ng langit.
            ["kkim"] = 40005023, // Mateo 5:23 — Kaya't kung inihahandog mo ang iyong hain sa dambana, at doo'y maalaala mo na ang iyong kapatid ay mayroong anomang laban sa iyo,
            ["ksks"] = 40005026, // Mateo 5:26 — Katotohanang sinasabi ko sa iyo, Hindi ka aalis doon sa anomang paraan, hanggang hindi mo mapagbayaran ang katapustapusang beles.
            ["nnsh"] = 40005027, // Mateo 5:27 — Narinig ninyong sinabi, Huwag kang mangangalunya:
            ["akak"] = 40005029, // Mateo 5:29 — At kung ang kanan mong mata ay nakapagpapatisod sa iyo, ay dukitin mo, at iyong itapon: sapagka't may mapapakinabang ka pa na mawala ang isa sa mga sangkap ng iyong katawan, at huwag ang buong katawan mo ay mabulid sa impierno.
            ["srna"] = 40005031, // Mateo 5:31 — Sinabi rin naman, Ang sinomang lalake na ihiwalay na ang kaniyang asawa, ay bigyan niya siya ng kasulatan ng paghihiwalay:
            ["kals"] = 40005035, // Mateo 5:35 — Kahit ang lupa, sapagka't siyang tungtungan ng kaniyang mga paa; kahit ang Jerusalem, sapagka't siyang bayan ng dakilang Hari.
            ["kmau"] = 40005036, // Mateo 5:36 — Kahit man ang ulo mo ay huwag mong ipanumpa, sapagka't hindi ka makagagawa ng isang buhok na maputi o maitim.
            ["damp"] = 40005037, // Mateo 5:37 — Datapuwa't ang magiging pananalita ninyo'y, Oo, oo; Hindi, hindi; sapagka't ang humigit pa rito ay buhat sa masama.
            ["dpia"] = 40006003, // Mateo 6:3 — Datapuwa't pagka ikaw ay naglilimos, ay huwag maalaman ng iyong kaliwang kamay ang ginagawa ng iyong kanang kamay:
            ["aasi"] = 40006027, // Mateo 6:27 — At alin sa inyo ang sa pagkabalisa ay makapagdaragdag ng isang siko sa sukat ng kaniyang buhay?
            ["atsp"] = 40006028, // Mateo 6:28 — At tungkol sa pananamit, bakit kayo nangababalisa? Wariin ninyo ang mga lirio sa parang, kung paanong nagsisilaki; hindi nangagpapagal, ni nangagsusulid man:
            ["gmsk"] = 40006029, // Mateo 6:29 — Gayon ma'y sinasabi ko sa inyo, na kahit si Salomon man sa buong kaluwalhatian niya ay hindi nakapaggayak na gaya ng isa sa mga ito.
            ["nkpn"] = 40006030, // Mateo 6:30 — Nguni't kung pinararamtan ng Dios ng ganito ang damo sa parang, na ngayon ay buhay, at sa kinabukasa'y iginagatong sa kalan, hindi baga lalonglalo na kayong pararamtan niya, Oh kayong mga kakaunti ang pananampalataya?
            ["khni"] = 40006034, // Mateo 6:34 — Kaya't huwag ninyong ikabalisa ang sa araw ng bukas: sapagka't ang araw ng bukas ay mababalisa sa kaniyang sarili. Sukat na sa kaarawan ang kaniyang kasamaan.
            ["inma"] = 40007005, // Mateo 7:5 — Ikaw na mapagpaimbabaw, alisin mo muna ang tahilan sa iyong sariling mata; at kung magkagayo'y makikita mong malinaw ang pag-aalis mo ng puwing sa mata ng iyong kapatid.
            ["hnis"] = 40007006, // Mateo 7:6 — Huwag ninyong ibigay sa mga aso ang anomang banal, ni ihagis man ang inyong mga perlas sa harap ng mga baboy, baka yurakan ng kanilang mga paa, at mangagbalik at kayo'y lapain.
            ["oats"] = 40007009, // Mateo 7:9 — O anong tao sa inyo, ang kung siya'y hingan ng tinapay ng kaniyang anak, ay bato ang ibibigay;
            ["okhs"] = 40007010, // Mateo 7:10 — O kung hingan siya ng isda, ay bibigyan niya ng ahas?
            ["kknb"] = 40007011, // Mateo 7:11 — Kung kayo nga, bagaman masasama ay marurunong mangagbigay ng mabubuting kaloob sa inyong mga anak, gaano pa kaya ang inyong Ama na nasa langit na magbibigay ng mabubuting bagay sa nagsisihingi sa kaniya?
            ["kmsm"] = 40007013, // Mateo 7:13 — Kayo'y magsipasok sa makipot na pintuan: sapagka't maluwang ang pintuan, at malapad ang daang patungo sa pagkapahamak, at marami ang doo'y nagsisipasok.
            ["smap"] = 40007014, // Mateo 7:14 — Sapagka't makipot ang pintuan, at makitid ang daang patungo sa buhay, at kakaunti ang nangakakasumpong noon.
            ["skmb"] = 40007016, // Mateo 7:16 — Sa kanilang mga bunga ay inyong mangakikilala sila. Nakapuputi baga ng mga ubas sa mga tinikan, o ng mga igos sa mga dawagan?
            ["gdna"] = 40007017, // Mateo 7:17 — Gayon din naman ang bawa't mabuting punong kahoy ay nagbubunga ng mabuti; datapuwa't ang masamang punong kahoy ay nagbubunga ng masama.
            ["hmna"] = 40007018, // Mateo 7:18 — Hindi maaari na ang mabuting punong kahoy ay magbunga ng masama, at ang masamang punong kahoy ay magbunga ng mabuti.
            ["habn"] = 40007021, // Mateo 7:21 — Hindi ang bawa't nagsasabi sa akin, Panginoon, Panginoon, ay papasok sa kaharian ng langit; kundi ang gumaganap ng kalooban ng aking Ama na nasa langit.
            ["akmi"] = 40007023, // Mateo 7:23 — At kung magkagayo'y ipahahayag ko sa kanila, Kailan ma'y hindi ko kayo nangakilala: magsilayo kayo sa akin, kayong manggagawa ng katampalasanan.
            ["kabd"] = 40007024, // Mateo 7:24 — Kaya't ang bawa't dumirinig ng aking mga salitang ito at ginaganap, ay matutulad sa isang taong matalino, na itinayo ang kaniyang bahay sa ibabaw ng bato:
            ["alau"] = 40007025, // Mateo 7:25 — At lumagpak ang ulan, at bumaha, at humihip ang mga hangin, at hinampas ang bahay na yaon; at hindi nabagsak: sapagka't natatayo sa ibabaw ng bato.
            ["aabd"] = 40007026, // Mateo 7:26 — At ang bawa't dumirinig ng aking mga salitang ito at hindi ginaganap, ay matutulad sa isang taong mangmang, na itinayo ang kaniyang bahay sa buhanginan:
            ["sskt"] = 40007029, // Mateo 7:29 — Sapagka't sila'y kaniyang tinuturuang tulad sa may kapamahalaan, at hindi gaya ng kanilang mga eskriba.
            ["ansb"] = 40008001, // Mateo 8:1 — At nang siya'y bumaba mula sa bundok, ay sinundan siya ng lubhang maraming tao.
            ["anls"] = 40008002, // Mateo 8:2 — At narito, lumapit sa kaniya ang isang ketongin, at siya'y sinamba, na nagsasabi, Panginoon, kung ibig mo, ay maaaring malinis mo ako.
            ["anpa"] = 40008006, // Mateo 8:6 — At nagsasabi, Panginoon, ang aking alila ay nararatay sa bahay, lumpo, at lubhang nahihirapan.
            ["sarn"] = 40008009, // Mateo 8:9 — Sapagka't ako rin naman ay taong nasa ilalim ng kapamahalaan, na may nasasakupan akong mga kawal: at sinasabi ko rito, Yumaon ka, at siya'y yumayaon; at sa isa, Halika, at siya'y lumalapit; at sa aking alipin, Gawin mo ito, at kaniyang ginagawa.
            ["anmi"] = 40008010, // Mateo 8:10 — At nang marinig ito ni Jesus, ay nagtaka siya, at sinabi sa nagsisisunod, Katotohanang sinasabi ko sa inyo, na kahit sa Israel man, ay hindi ako nakasumpong ng ganito kalaking pananampalataya.
            ["dama"] = 40008012, // Mateo 8:12 — Datapuwa't ang mga anak ng kaharian ay pawang itatapon sa kadiliman sa labas: diyan na nga ang pagtangis at pagngangalit ng mga ngipin.
            ["anps"] = 40008014, // Mateo 8:14 — At nang pumasok si Jesus sa bahay ni Pedro, ay nakita niya ang biyanang babae nito na nararatay dahil sa lagnat.
            ["ahak"] = 40008015, // Mateo 8:15 — At hinipo ang kaniyang kamay, at inibsan siya ng lagnat; at siya'y nagbangon, at naglingkod sa kaniya.
            ["alai"] = 40008019, // Mateo 8:19 — At lumapit ang isang eskriba, at sa kaniya'y nagsabi, Guro, susunod ako sa iyo saan ka man pumaroon.
            ["dssk"] = 40008022, // Mateo 8:22 — Datapuwa't sinabi sa kaniya ni Jesus, Sumunod ka sa akin; at pabayaan mong ilibing ng mga patay ang kanilang sariling mga patay.
            ["anba"] = 40008024, // Mateo 8:24 — At narito, bumangon ang isang malakas na bagyo sa dagat, na ano pa't inaapawan ang daong ng mga alon: datapuwa't siya'y natutulog.
            ["aamt"] = 40008027, // Mateo 8:27 — At ang mga tao ay nangagtaka, na sinasabi, Anong tao ito, na maging ang mga hangin at ang mga dagat ay nagsisitalima sa kaniya?
            ["smsk"] = 40008030, // Mateo 8:30 — Sa malayo sa kanila ay may isang kawan ng maraming baboy na nagsisipanginain.
            ["anla"] = 40008034, // Mateo 8:34 — At narito, lumabas ang buong bayan upang sumalubong kay Jesus: at pagkakita nila sa kaniya, ay pinamanhikan siyang umalis sa kanilang mga hangganan.
            ["saba"] = 40009005, // Mateo 9:5 — Sapagka't alin baga ang lalong magaang sabihin, Ipinatatawad na ang iyong mga kasalanan; o sabihin, Magtindig ka, at lumakad ka?
            ["dumn"] = 40009006, // Mateo 9:6 — Datapuwa't upang maalaman ninyo na ang Anak ng tao'y may kapamahalaan sa lupa na magpatawad ng mga kasalanan (sinabi nga niya sa lumpo), Magtindig ka, buhatin mo ang iyong higaan, at umuwi ka sa iyong bahay.
            ["dnmi"] = 40009008, // Mateo 9:8 — Datapuwa't nang makita ito ng karamihan, ay nangatakot sila, at kanilang niluwalhati ang Dios, na nagbigay ng gayong kapamahalaan sa mga tao.
            ["apdn"] = 40009009, // Mateo 9:9 — At pagdaraan doon ni Jesus, ay nakita niya ang isang tao, na kung tawagi'y Mateo, na nakaupo sa paningilan ng buwis: at sinabi niya sa kaniya, Sumunod ka sa akin. At siya'y nagtindig, at sumunod sa kaniya.
            ["dnim"] = 40009012, // Mateo 9:12 — Datapuwa't nang ito'y marinig niya, ay kaniyang sinabi, Ang mga walang sakit ay hindi nangangailangan ng manggagamot, kundi ang mga may sakit.
            ["dmka"] = 40009013, // Mateo 9:13 — Datapuwa't magsihayo kayo at inyong pagaralan kung ano ang kahulugan nito, Habag ang ibig ko, at hindi hain: sapagka't hindi ako naparito upang tumawag ng mga matuwid, kundi ng mga makasalanan.
            ["ashn"] = 40009016, // Mateo 9:16 — At sinoma'y hindi nagtatagpi ng bagong kayo sa damit na luma; sapagka't ang tagpi ay bumabatak sa damit, at lalong lumalala ang punit.
            ["hrnn"] = 40009017, // Mateo 9:17 — Hindi rin nagsisilid ng bagong alak sa mga balat na luma: sa ibang paraan ay nangagpuputok ang mga balat, at nangabububo ang alak, at nangasisira ang mga balat: kundi isinisilid ang bagong alak sa mga bagong balat, at kapuwa nagsisitagal.
            ["ssna"] = 40009018, // Mateo 9:18 — Samantalang sinasalita niya ang mga bagay na ito sa kanila, narito, dumating ang isang pinuno, at siya'y sinamba, na nagsasabi, Kamamatay pa lamang ng aking anak na babae: datapuwa't halina at ipatong mo ang iyong kamay sa kaniya, at siya'y mabubuhay.
            ["asja"] = 40009019, // Mateo 9:19 — At si Jesus ay nagtindig, at sumama sa kaniya, pati ng kaniyang mga alagad.
            ["anib"] = 40009020, // Mateo 9:20 — At narito, isang babaing inaagasang may labingdalawang taon na, ay lumapit sa kaniyang likuran, at hinipo ang laylayan ng kaniyang damit:
            ["ssns"] = 40009021, // Mateo 9:21 — Sapagka't sinabi niya sa kaniyang kalooban, Kung mahipo ko man lamang ang kaniyang damit, ay gagaling ako.
            ["dpnj"] = 40009022, // Mateo 9:22 — Datapuwa't paglingon ni Jesus at pagkakita sa kaniya, ay sinabi, Anak, laksan mo ang iyong loob; pinagaling ka ng iyong pananampalataya. At gumaling ang babae mula sa oras na yaon.
            ["asnm"] = 40009024, // Mateo 9:24 — Ay sinabi niya, Magparaan kayo: sapagka't hindi patay ang dalaga, kundi natutulog. At tinawanan nila siya na nililibak.
            ["akap"] = 40009026, // Mateo 9:26 — At kumalat ang pagkabantog na ito sa buong lupang yaon.
            ["nmkh"] = 40009029, // Mateo 9:29 — Nang magkagayo'y kaniyang hinipo ang mga mata nila, na sinasabi, Alinsunod sa inyong pananampalataya ay siyang mangyari sa inyo.
            ["dsnm"] = 40009034, // Mateo 9:34 — Datapuwa't sinabi ng mga Fariseo, sa pamamagitan ng prinsipe ng mga demonio ay nagpapalabas siya ng mga demonio.
            ["inns"] = 40009038, // Mateo 9:38 — Idalangin nga ninyo sa Panginoon ng aanihin, na magpadala siya ng mga manggagawa sa kaniyang aanihin.
            ["sfas"] = 40010003, // Mateo 10:3 — Si Felipe, at si Bartolome; si Tomas, at si Mateo na maniningil ng buwis; si Santiago na anak ni Alfeo at si Tadeo;
            ["ssnc"] = 40010004, // Mateo 10:4 — Si Simon na Cananeo, at si Judas Iscariote, na siya ring sa kaniya'y nagkanulo.
            ["alis"] = 40010005, // Mateo 10:5 — Ang labingdalawang ito'y sinugo ni Jesus, at sila'y pinagbilinan, na sinasabi, Huwag kayong magsitungo sa alin mang daan ng mga Gentil, at huwag kayong magsipasok sa alin mang bayan ng mga taga Samaria:
            ["kbmk"] = 40010006, // Mateo 10:6 — Kundi bagkus magsiparoon kayo sa mga tupang nangawaglit sa bahay ni Israel.
            ["askn"] = 40010007, // Mateo 10:7 — At samantalang kayo'y nangaglalakad, ay magsipangaral kayo, na mangagsabi, Ang kaharian ng langit ay malapit na.
            ["mknm"] = 40010008, // Mateo 10:8 — Mangagpagaling kayo ng mga may sakit, mangagpabangon kayo ng mga patay, mangaglinis kayo ng mga ketong, mangagpalabas kayo ng mga demonio: tinanggap ninyong walang bayad, ay ibigay ninyong walang bayad.
            ["ksnp"] = 40010010, // Mateo 10:10 — Kahit supot ng pagkain sa paglalakad, kahit dalawang tunika, kahit mga pangyapak, o tungkod: sapagka't ang manggagawa ay karapatdapat sa kaniyang pagkain.
            ["asht"] = 40010014, // Mateo 10:14 — At sinomang hindi tumanggap sa inyo, ni duminig sa inyong mga pananalita, pagalis ninyo sa bahay o bayang yaon, ay ipagpag ninyo ang alabok ng inyong mga paa.
            ["nskk"] = 40010016, // Mateo 10:16 — Narito, sinusugo ko kayong gaya ng mga tupa sa gitna ng mga lobo: mangagpakatalino nga kayong gaya ng mga ahas at mangagpakatimtimang gaya ng mga kalapati.
            ["dmks"] = 40010017, // Mateo 10:17 — Datapuwa't mangagpakaingat kayo sa mga tao: sapagka't kayo'y ibibigay nila sa mga Sanedrin at kayo'y hahampasin sa kanilang mga sinagoga;
            ["oakd"] = 40010018, // Mateo 10:18 — Oo at kayo'y dadalhin sa harap ng mga gobernador at mga hari dahil sa akin, sa pagpapatotoo sa kanila at sa mga Gentil.
            ["dpki"] = 40010019, // Mateo 10:19 — Datapuwa't pagka kayo'y ibinigay nila, huwag ninyong ikabalisa kung paano o kung ano ang inyong sasabihin: sapagka't sa oras na yaon ay ipagkakaloob sa inyo ang inyong sasabihin.
            ["shka"] = 40010020, // Mateo 10:20 — Sapagka't hindi kayo ang mangagsasalita, kundi ang Espiritu ng inyong Ama ang sa inyo'y magsasalita.
            ["akkn"] = 40010022, // Mateo 10:22 — At kayo'y kapopootan ng lahat ng mga tao dahil sa aking pangalan: datapuwa't ang magtitiis hanggang sa wakas, ay siyang maliligtas.
            ["dpkp"] = 40010023, // Mateo 10:23 — Datapuwa't pagka kayo'y pinagusig nila sa isang bayang ito, ay magsitakas kayo tungo sa kasunod na bayan: sapagka't sa katotohanang sinasabi ko sa inyo, Hindi ninyo matatapos libutin ang mga bayan ng Israel, hanggang sa pumarito ang Anak ng tao.
            ["hmaa"] = 40010024, // Mateo 10:24 — Hindi mataas ang alagad sa kaniyang guro, ni hindi rin mataas ang alila sa kaniyang panginoon.
            ["hnns"] = 40010026, // Mateo 10:26 — Huwag nga ninyo silang katakutan: sapagka't walang bagay na natatakpan, na hindi mahahayag; at natatago na hindi malalaman.
            ["hbin"] = 40010029, // Mateo 10:29 — Hindi baga ipinagbibili ng isang beles ang dalawang maya? at kahit isa sa kanila'y hindi mahuhulog sa lupa kung hindi pahintulot ng inyong Ama:
            ["kabk"] = 40010032, // Mateo 10:32 — Kaya't ang bawa't kumikilala sa akin sa harap ng mga tao, ay kikilalanin ko naman siya sa harap ng aking Ama na nasa langit.
            ["sanu"] = 40010035, // Mateo 10:35 — Sapagka't ako'y naparito upang papagalitin ang lalake laban sa kaniyang ama, at ang anak na babae laban sa kaniyang ina, at ang manugang na babae laban sa kaniyang biyanang babae:
            ["aamk"] = 40010036, // Mateo 10:36 — At ang magiging kaaway ng tao ay ang kaniya ring sariling kasangbahay.
            ["ausa"] = 40010037, // Mateo 10:37 — Ang umiibig sa ama o sa ina ng higit kay sa akin ay hindi karapatdapat sa akin; at ang umiibig sa anak na lalake o anak na babae ng higit kay sa akin ay hindi karapatdapat sa akin.
            ["aahn"] = 40010038, // Mateo 10:38 — At ang hindi nagpapasan ng kaniyang krus at sumusunod sa akin, ay hindi karapatdapat sa akin.
            ["annk"] = 40010039, // Mateo 10:39 — Ang nakasusumpong ng kaniyang buhay ay mawawalan nito; at ang mawalan ng buhay dahil sa akin ay makasusumpong niyaon.
            ["asms"] = 40010042, // Mateo 10:42 — At sinomang magpainom sa isa sa maliliit na ito ng kahit isang sarong tubig na malamig, dahil sa pangalang alagad, katotohanang sinasabi ko sa inyo na hindi mawawala ang ganti sa kaniya.
            ["assj"] = 40011004, // Mateo 11:4 — At sumagot si Jesus at sa kanila'y sinabi, Magsiparoon kayo at sabihin ninyo kay Juan ang mga bagay na inyong nangaririnig at nangakikita:
            ["amas"] = 40011006, // Mateo 11:6 — At mapalad ang sinomang hindi makasumpong ng anomang katitisuran sa akin.
            ["daan"] = 40011008, // Mateo 11:8 — Datapuwa't ano ang nilabas ninyo upang makita? isang taong nararamtan ng mga damit na maseselan? Narito, ang mga nagsisipanamit ng maseselan ay nangasa mga bahay ng mga hari.
            ["iyts"] = 40011010, // Mateo 11:10 — Ito yaong tungkol sa kaniya'y nasusulat, Narito, sinusugo ko ang aking sugo sa unahan ng iyong mukha, Na maghahanda ng iyong daan sa unahan mo.
            ["amsm"] = 40011012, // Mateo 11:12 — At mula sa mga araw ni Juan Bautista hanggang ngayon, ang kaharian ng langit ay nagbabata ng karahasan, at kinukuha nang sapilitan ng mga taong mararahas.
            ["ammp"] = 40011015, // Mateo 11:15 — Ang may mga pakinig upang ipakinig, ay makinig.
            ["dsak"] = 40011016, // Mateo 11:16 — Datapuwa't sa ano ko itutulad ang lahing ito? Tulad sa mga batang nangakaupo sa mga pamilihan, na sinisigawan ang kanilang mga kasama.
            ["astn"] = 40011017, // Mateo 11:17 — At sinasabi, Tinutugtugan namin kayo ng plauta, at hindi kayo nagsisisayaw; nagsipanambitan kami, at hindi kayo nangahapis.
            ["snsj"] = 40011018, // Mateo 11:18 — Sapagka't naparito si Juan na hindi kumakain o umiinom man, at sinasabi nila, Siya'y mayroong demonio.
            ["naan"] = 40011019, // Mateo 11:19 — Naparito ang Anak ng tao na kumakain at umiinom, at sinasabi nila, Narito, ang isang matakaw na tao at isang manginginom ng alak, isang kaibigan ng mga maniningil ng buwis at ng mga makasalanan! At ang karunungan ay inaaring-ganap ng kaniyang mga gawa.
            ["nmkp"] = 40011020, // Mateo 11:20 — Nang magkagayo'y kaniyang pinasimulang sumbatan ang mga bayan na pinaggagawan niya ng lalong marami sa kaniyang mga gawang makapangyarihan, sapagka't hindi sila nangagsisi.
            ["samc"] = 40011021, // Mateo 11:21 — Sa aba mo, Corazin! sa aba mo, Bethsaida! sapagka't kung sa Tiro at sa Sidon sana ginawa ang mga gawang makapangyarihan na ginawa sa inyo, malaon na dising nangagsisi na may mga damit na magaspang at abo.
            ["nsks"] = 40011022, // Mateo 11:22 — Nguni't sinasabi ko sa inyo na higit na mapagpapaumanhinan ang Tiro at Sidon sa araw ng paghuhukom, kay sa inyo.
            ["aicm"] = 40011023, // Mateo 11:23 — At ikaw, Capernaum, magpapakataas ka baga hanggang sa langit? ibababa ka hanggang sa Hades: sapagka't kung sa Sodoma sana ginawa ang mga makapangyarihang gawang sa iyo'y ginawa, ay nanatili sana siya hanggang sa araw na ito.
            ["npya"] = 40011025, // Mateo 11:25 — Nang panahong yaon ay sumagot si Jesus at sinabi, Ako'y nagpapasalamat sa iyo, Oh Ama, Panginoon ng langit at ng lupa, na iyong inilihim ang mga bagay na ito sa mga pantas at matatalino, at ipinahayag mo sa mga sanggol:
            ["onas"] = 40011026, // Mateo 11:26 — Oo nga, Ama, sapagka't gayon ang nakalugod sa iyong paningin.
            ["pnaa"] = 40011029, // Mateo 11:29 — Pasanin ninyo ang aking pamatok, at magaral kayo sa akin; sapagka't ako'y maamo at mapagpakumbabang puso: at masusumpungan ninyo ang kapahingahan ng inyong mga kaluluwa.
            ["smaa"] = 40011030, // Mateo 11:30 — Sapagka't malambot ang aking pamatok, at magaan ang aking pasan.
            ["dpnn"] = 40012002, // Mateo 12:2 — Datapuwa't pagkakita nito ng mga Fariseo, ay sinabi nila sa kaniya, Tingnan mo, ginagawa ng mga alagad mo ang hindi matuwid na gawin sa sabbath.
            ["kpsp"] = 40012004, // Mateo 12:4 — Kung paanong siya'y pumasok sa bahay ng Dios, at kumain siya ng mga tinapay na handog, na hindi matuwid na kanin niya, ni ng mga kasamahan niya, kundi ng mga saserdote lamang?
            ["dknn"] = 40012007, // Mateo 12:7 — Datapuwa't kung nalalaman ninyo kung ano ang kahulugan nito, Habag ang ibig ko, at hindi hain, ay hindi sana ninyo hinatulan ang mga walang kasalanan.
            ["asud"] = 40012009, // Mateo 12:9 — At siya'y umalis doon at pumasok sa sinagoga nila:
            ["gpna"] = 40012012, // Mateo 12:12 — Gaano pa nga ang isang tao na may halaga kay sa isang tupa! Kaya't matuwid na gumawa ng mabuti sa araw ng sabbath.
            ["dnam"] = 40012014, // Mateo 12:14 — Datapuwa't nagsialis ang mga Fariseo, at nangagpulong laban sa kaniya, kung papaanong siya'y maipapupuksa nila.
            ["apnn"] = 40012015, // Mateo 12:15 — At pagkahalata nito ni Jesus ay lumayo roon: at siya'y sinundan ng marami; at kaniyang pinagaling silang lahat,
            ["nalk"] = 40012018, // Mateo 12:18 — Narito, ang lingkod ko na aking hinirang; At minamahal ko na kinalulugdan ng aking kaluluwa: Isasakaniya ko ang aking Espiritu, At ihahayag niya ang paghuhukom sa mga Gentil.
            ["hsmn"] = 40012019, // Mateo 12:19 — Hindi siya makikipagtalo, ni sisigaw; Ni maririnig man ng sinoman ang kaniyang tinig sa mga lansangan.
            ["hnba"] = 40012020, // Mateo 12:20 — Hindi niya babaliin ang tambong gapok, At hindi papatayin ang timsim na umuusok, Hanggang sa papagtagumpayin ang paghuhukom.
            ["aask"] = 40012021, // Mateo 12:21 — At aasa sa kaniyang pangalan ang mga Gentil.
            ["aabk"] = 40012023, // Mateo 12:23 — At ang buong karamihan ay nangagtaka, at nangagsabi, Ito kaya ang Anak ni David?
            ["akpn"] = 40012026, // Mateo 12:26 — At kung pinalalabas ni Satanas si Satanas, siya nababahagi laban sa kaniyang sarili; papaano ngang mananatili ang kaniyang kaharian?
            ["aksp"] = 40012027, // Mateo 12:27 — At kung sa pamamagitan ni Beelzebub ay nagpapalabas ako ng mga demonio, ang inyong mga anak sa kaninong pamamagitan sila'y pinalalabas? kaya nga sila ang inyong magiging mga hukom.
            ["nksp"] = 40012028, // Mateo 12:28 — Nguni't kung sa pamamagitan ng Espiritu ng Dios nagpapalabas ako ng mga demonio, ay dumating nga sa inyo ang kaharian ng Dios.
            ["opbm"] = 40012029, // Mateo 12:29 — O papaano bagang makapapasok ang sinoman sa bahay ng malakas, at samsamin ang kaniyang mga pag-aari, kung hindi muna gapusin ang malakas? at kung magkagayo'y masasamsaman niya ang kaniyang bahay.
            ["ahsa"] = 40012030, // Mateo 12:30 — Ang hindi sumasa akin ay laban sa akin; at ang hindi nagiimpok na kasama ko ay nagsasambulat.
            ["aasm"] = 40012032, // Mateo 12:32 — At ang sinomang magsalita ng isang salitang laban sa Anak ng tao, ay ipatatawad sa kaniya; datapuwa't ang sinomang magsalita laban sa Espiritu Santo, ay hindi ipatatawad sa kaniya, kahit sa sanglibutang ito, o maging sa darating.
            ["opna"] = 40012033, // Mateo 12:33 — O pabutihin ninyo ang punong kahoy, at mabuti ang bunga niyaon; o pasamain ninyo ang punong kahoy, at masama ang bunga niyaon: sapagka't ang punong kahoy ay nakikilala sa pamamagitan ng kaniyang bunga.
            ["klnm"] = 40012034, // Mateo 12:34 — Kayong lahi ng mga ulupong, papaano kayo na masasama, ay makapagsasalita ng mabubuting bagay? sapagka't sa kasaganaan ng puso ay nagsasalita ang bibig.
            ["amts"] = 40012035, // Mateo 12:35 — Ang mabuting tao sa kaniyang mabuting kayamanan ay kumukuha ng mabubuting bagay: at ang masamang tao sa kaniyang masamang kayamanan ay kumukuha ng masasamang bagay.
            ["ssim"] = 40012037, // Mateo 12:37 — Sapagka't sa iyong mga salita ikaw ay magiging banal, at sa iyong mga salita ay hahatulan ka.
            ["skps"] = 40012040, // Mateo 12:40 — Sapagka't kung paanong si Jonas ay napasa tiyan ng isang balyena na tatlong araw at tatlong gabi; ay gayon ding mapapasa ilalim ng lupa na tatlong araw at tatlong gabi ang Anak ng tao.
            ["mspa"] = 40012041, // Mateo 12:41 — Magsisitayo sa paghuhukom ang mga tao sa Nineve na kasama ng lahing ito, at ito'y hahatulan: sapagka't sila'y nagsipagsisi sa pangangaral ni Jonas; at narito, dito'y may isang lalong dakila kay sa kay Jonas.
            ["dakn"] = 40012043, // Mateo 12:43 — Datapuwa't ang karumaldumal na espiritu, kung siya'y lumabas sa tao, ay lumalakad sa mga dakong walang tubig na humahanap ng kapahingahan, at hindi makasumpong.
            ["kmsn"] = 40012044, // Mateo 12:44 — Kung magkagayo'y sinasabi niya, Babalik ako sa aking bahay na nilabasan ko; at pagdating niya, ay nasusumpungan niyang walang laman, walis na, at nagagayakan.
            ["kmys"] = 40012045, // Mateo 12:45 — Kung magkagayo'y yumayaon siya, at nagsasama ng pito pang espiritu na lalong masasama kay sa kaniya, at sila'y nagsisipasok at nagsisitahan doon: at nagiging lalo pang masama ang huling kalagayan ng taong yaon kay sa una. Gayon din ang mangyayari sa masamang lahing ito.
            ["ssnp"] = 40012046, // Mateo 12:46 — Samantalang siya'y nagsasalita pa sa mga karamihan, narito, ang kaniyang ina at ang kaniyang mga kapatid ay nangakatayo sa labas, at ibig nilang siya'y makausap.
            ["amns"] = 40012047, // Mateo 12:47 — At may nagsabi sa kaniya, Narito, ang iyong ina at ang iyong mga kapatid ay nangakatayo sa labas, na ibig nilang makausap ka.
            ["nssa"] = 40012048, // Mateo 12:48 — Nguni't siya'y sumagot at sinabi sa nagsabi sa kaniya, Sino ang aking ina? at sino-sino ang aking mga kapatid?
            ["ssgn"] = 40012050, // Mateo 12:50 — Sapagka't sinomang gumaganap ng kalooban ng aking Ama na nasa langit, ay siyang aking kapatid na lalake at aking kapatid na babae, at ina.
            ["nany"] = 40013001, // Mateo 13:1 — Nang araw na yaon ay lumabas si Jesus sa bahay, at naupo sa tabi ng dagat.
            ["aamm"] = 40013009, // Mateo 13:9 — At ang may mga pakinig, ay makinig.
            ["ssma"] = 40013012, // Mateo 13:12 — Sapagka't sinomang mayroon ay bibigyan, at siya'y magkakaroon ng sagana: nguni't sinomang wala, pati ng nasa kaniya ay aalisin sa kaniya.
            ["kspk"] = 40013013, // Mateo 13:13 — Kaya't sila'y pinagsasalitaan ko sa mga talinghaga; sapagka't nagsisitingin ay hindi sila nangakakakita, at nangakikinig ay hindi sila nangakakarinig, ni hindi sila nangakakaunawa.
            ["dmai"] = 40013016, // Mateo 13:16 — Datapuwa't mapapalad ang inyong mga mata, sapagka't nangakakakita; at ang iyong mga tainga, sapagka't nangakakarinig.
            ["pnna"] = 40013018, // Mateo 13:18 — Pakinggan nga ninyo ang talinghaga tungkol sa manghahasik.
            ["aans"] = 40013020, // Mateo 13:20 — At ang nahasik sa mga batuhan, ay yaong nakikinig ng salita, at pagdaka'y tinatanggap ito ng buong galak;
            ["gmws"] = 40013021, // Mateo 13:21 — Gayon ma'y wala siyang ugat sa kaniyang sarili, kundi sangdaling tumatagal; at pagdating ng kapighatian o pag-uusig dahil sa salita, ay pagdaka'y natitisod siya.
            ["snsk"] = 40013024, // Mateo 13:24 — Sinaysay niya sa kanila ang ibang talinghaga, na sinasabi, Ang kaharian ng langit ay tulad sa isang taong naghasik ng mabuting binhi sa kaniyang bukid:
            ["dnsa"] = 40013026, // Mateo 13:26 — Datapuwa't nang sumibol ang usbong at mamunga, ay lumitaw nga rin ang mga pangsirang damo.
            ["dsnh"] = 40013029, // Mateo 13:29 — Datapuwa't sinabi niya, Huwag; baka sa pagtitipon ninyo ng mga pangsirang damo, ay inyong mabunot pati ng trigo.
            ["pnmk"] = 40013030, // Mateo 13:30 — Pabayaan ninyong magsitubo kapuwa hanggang sa panahon ng pagaani: at sa panahon ng pagaani ay sasabihin ko sa mga mangaani, Tipunin muna ninyo ang mga pangsirang damo, at inyong pagbibigkisin upang sunugin; datapuwa't tipunin ninyo ang trigo sa aking bangan.
            ["nsnl"] = 40013032, // Mateo 13:32 — Na siya ngang lalong maliit sa lahat ng mga binhi; datapuwa't nang tumubo, ay lalong malaki kay sa mga gulay, at nagiging punong kahoy, ano pa't nagsisiparoon ang mga ibon sa langit at sumisilong sa kaniyang mga sanga.
            ["nmai"] = 40013036, // Mateo 13:36 — Nang magkagayon ay iniwan niya ang mga karamihan, at pumasok sa bahay: at sa kaniya'y nagsilapit ang kaniyang mga alagad, na nagsisipagsabi, Ipaliwanag mo sa amin ang talinghaga tungkol sa mga pangsirang damo sa bukid.
            ["aaba"] = 40013038, // Mateo 13:38 — At ang bukid ay ang sanglibutan; at ang mabuting binhi, ay ito ang mga anak ng kaharian: at ang mga pangsirang damo ay ang mga anak ng masama;
            ["kpap"] = 40013040, // Mateo 13:40 — Kung paano ang pagtipon sa mga pangsirang damo at pagsunog sa apoy; gayon din ang mangyayari sa katapusan ng sanglibutan.
            ["snan"] = 40013041, // Mateo 13:41 — Susuguin ng Anak ng tao ang kaniyang mga anghel, at kanilang titipunin sa labas ng kaniyang kaharian ang lahat ng mga bagay na nangakapagpapatisod, at ang nagsisigawa ng katampalasanan,
            ["asis"] = 40013042, // Mateo 13:42 — At sila'y igagatong sa kalan ng apoy: diyan na nga ang pagtangis at ang pagngangalit ng mga ngipin.
            ["kmma"] = 40013043, // Mateo 13:43 — Kung magkagayo'y mangagliliwanag ang mga matuwid na katulad ng araw sa kaharian ng kanilang Ama. Ang may mga pakinig, ay makinig.
            ["takn"] = 40013044, // Mateo 13:44 — Tulad ang kaharian ng langit sa natatagong kayamanan sa bukid; na nasumpungan ng isang tao, at inilihim; at sa kaniyang kagalaka'y yumaon at ipinagbili ang lahat niyang tinatangkilik, at binili ang bukid na yaon.
            ["apni"] = 40013046, // Mateo 13:46 — At pagkasumpong ng isang mahalagang perlas, ay yumaon siya at ipinagbili ang lahat niyang tinatangkilik, at binili yaon.
            ["tdna"] = 40013047, // Mateo 13:47 — Tulad din naman ang kaharian ng langit sa isang lambat, na inihulog sa dagat, na nakahuli ng sarisaring isda:
            ["nnma"] = 40013048, // Mateo 13:48 — Na, nang mapuno, ay hinila nila sa pampang; at sila'y nagsiupo, at tinipon sa mga sisidlan ang mabubuti, datapuwa't itinapon ang masasama.
            ["gdam"] = 40013049, // Mateo 13:49 — Gayon din ang mangyayari sa katapusan ng sanglibutan: lalabas ang mga anghel, at ihihiwalay ang masasama sa matutuwid,
            ["nbna"] = 40013051, // Mateo 13:51 — Napagunawa baga ninyo ang lahat ng mga bagay na ito? Sinabi nila sa kaniya, Oo.
            ["hbia"] = 40013055, // Mateo 13:55 — Hindi baga ito ang anak ng anluwagi? hindi baga tinatawag na Maria ang kaniyang ina? at Santiago, at Jose, at Simon, at Judas ang kaniyang mga kapatid?
            ["ashg"] = 40013058, // Mateo 13:58 — At siya'y hindi gumawa roon ng maraming makapangyarihang gawa dahil sa kawalan nila ng pananampalataya.
            ["npyn"] = 40014001, // Mateo 14:1 — Nang panahong yao'y narinig ni Herodes na tetrarka ang balita tungkol kay Jesus,
            ["shnh"] = 40014003, // Mateo 14:3 — Sapagka't hinuli ni Herodes si Juan, at siya'y iginapos, at inilagay sa bilangguan dahil kay Herodias, na asawa ni Felipe na kaniyang kapatid.
            ["anin"] = 40014005, // Mateo 14:5 — At nang ibig niyang ipapatay siya, ay natakot siya sa karamihan, sapagka't siya'y kanilang ibinibilang na propeta.
            ["dnda"] = 40014006, // Mateo 14:6 — Datapuwa't nang dumating ang araw na kapanganakan kay Herodes, ay sumayaw sa gitna ang anak na babae ni Herodias, at kinalugdan ni Herodes.
            ["ddki"] = 40014007, // Mateo 14:7 — Dahil dito'y kaniyang ipinangakong may sumpa na sa kaniya'y ibibigay ang anomang hingin niya.
            ["asni"] = 40014008, // Mateo 14:8 — At siya, na inudyukan ng kaniyang ina, ay nagsabi, Ibigay mo sa akin dito na nasa isang pinggan ang ulo ni Juan Bautista.
            ["anah"] = 40014009, // Mateo 14:9 — At namanglaw ang hari; datapuwa't dahil sa kaniyang mga sumpa, at sa nangakaupong kasalo niya sa dulang, ay ipinagutos niyang ibigay na sa kaniya;
            ["adak"] = 40014011, // Mateo 14:11 — At dinala ang kaniyang ulo na nasa isang pinggan, at ibinigay sa dalaga: at dinala nito sa kaniyang ina.
            ["nmni"] = 40014013, // Mateo 14:13 — Nang marinig nga ito ni Jesus, ay lumigpit sila mula roon, sa isang daong na nasa isang dakong ilang na bukod: at nang mabalitaan ito ng mga karamihan, ay nangaglakad sila na sumunod sa kaniya mula sa mga bayan.
            ["ansl"] = 40014020, // Mateo 14:20 — At nagsikain silang lahat, at nangabusog: at kanilang pinulot ang lumabis na pinagputolputol, na labingdalawang bakol na puno.
            ["aamn"] = 40014021, // Mateo 14:21 — At ang mga nagsikain ay may limang libong lalake, bukod pa ang mga babae at ang mga bata.
            ["appn"] = 40014022, // Mateo 14:22 — At pagdaka'y pinapagmadali niya ang kaniyang mga alagad na magsilulan sa daong, at magsiuna sa kaniya sa kabilang ibayo, hanggang pinayayaon niya ang mga karamihan.
            ["dada"] = 40014024, // Mateo 14:24 — Datapuwa't ang daong ay nasa gitna na ng dagat, na hinahampas ng mga alon; sapagka't pasalungat sa hangin.
            ["asin"] = 40014025, // Mateo 14:25 — At sa ikaapat na pagpupuyat ng gabi ay naparoon siya sa kanila, na lumalakad sa ibabaw ng dagat.
            ["dpns"] = 40014027, // Mateo 14:27 — Datapuwa't pagdaka'y nagsalita sa kanila si Jesus, na nagsasabi, Laksan ninyo ang inyong loob; ako nga: huwag kayong mangatakot.
            ["dpnm"] = 40014030, // Mateo 14:30 — Datapuwa't pagkakita niyang malakas ang hangin, ay natakot siya, at nang siya'y malulubog, ay sumigaw, na nagsasabi, Panginoon, iligtas mo ako.
            ["nmnk"] = 40015001, // Mateo 15:1 — Nang magkagayo'y nagsilapit kay Jesus na mula sa Jerusalem ang mga Fariseo at ang mga eskriba, na nagsisipagsabi,
            ["ssnd"] = 40015004, // Mateo 15:4 — Sapagka't sinabi ng Dios, Igalang mo ang iyong ama at ang iyong ina: at, Ang manungayaw sa ama at sa ina, ay mamatay siyang walang pagsala.
            ["ahni"] = 40015006, // Mateo 15:6 — Ay hindi niya igagalang ang kaniyang ama. At niwalan ninyong kabuluhan ang salita ng Dios dahil sa inyong sali't-saling sabi.
            ["kmmm"] = 40015007, // Mateo 15:7 — Kayong mga mapagpaimbabaw, mabuti ang pagkahula sa inyo ni Isaias, na nagsasabi,
            ["abii"] = 40015008, // Mateo 15:8 — Ang bayang ito'y iginagalang ako ng kanilang mga labi; Datapuwa't ang kanilang puso ay malayo sa akin.
            ["dwka"] = 40015009, // Mateo 15:9 — Datapuwa't walang kabuluhan ang pagsamba nila sa akin, Na nagtuturo ng kanilang pinakaaral ang mga utos ng mga tao.
            ["haps"] = 40015011, // Mateo 15:11 — Hindi ang pumapasok sa bibig ang siyang nakakahawa sa tao; kundi ang lumalabas sa bibig, ito ang nakakahawa sa tao.
            ["pnss"] = 40015014, // Mateo 15:14 — Pabayaan ninyo sila: sila'y mga bulag na tagaakay. At kung ang bulag ay umakay sa bulag, ay kapuwa sila mangahuhulog sa hukay.
            ["asnk"] = 40015016, // Mateo 15:16 — At sinabi niya, Kayo baga nama'y wala pa ring pagiisip?
            ["hpbn"] = 40015017, // Mateo 15:17 — Hindi pa baga ninyo nalalaman, na ang anomang pumapasok sa bibig ay tumutuloy sa tiyan, at inilalabas sa daanan ng dumi?
            ["damb"] = 40015018, // Mateo 15:18 — Datapuwa't ang mga bagay na lumalabas sa bibig ay sa puso nanggagaling; at siyang nangakakahawa sa tao.
            ["iamb"] = 40015020, // Mateo 15:20 — Ito ang mga bagay na nangakakahawa sa tao; datapuwa't ang kumaing hindi maghugas ng mga kamay ay hindi makakahawa sa tao.
            ["auds"] = 40015021, // Mateo 15:21 — At umalis doon si Jesus, at lumigpit sa mga sakop ng Tiro at Sidon.
            ["dshs"] = 40015023, // Mateo 15:23 — Datapuwa't siya'y hindi sumagot ng anomang salita sa kaniya. At nilapitan siya ng kaniyang mga alagad at siya'y pinamanhikan, na nangagsasabi, Paalisin mo siya; sapagka't nagsisisigaw siya sa ating hulihan.
            ["dlsa"] = 40015025, // Mateo 15:25 — Datapuwa't lumapit siya at siya'y sinamba niya, na nagsasabi, Panginoon, saklolohan mo ako.
            ["dsno"] = 40015027, // Mateo 15:27 — Datapuwa't sinabi niya, Oo, Panginoon: sapagka't ang mga aso man ay nagsisikain ng mga mumo na nangalalaglag mula sa dulang ng kanilang mga panginoon.
            ["ausj"] = 40015029, // Mateo 15:29 — At umalis si Jesus doon, at naparoon sa tabi ng dagat ng Galilea; at umahon sa bundok, at naupo doon.
            ["alsk"] = 40015030, // Mateo 15:30 — At lumapit sa kaniya ang lubhang maraming tao, na may mga pilay, mga bulag, mga pipi, mga pingkaw, at iba pang marami, at sila'y kanilang inilagay sa kaniyang mga paanan; at sila'y pinagaling niya:
            ["asun"] = 40016003, // Mateo 16:3 — At sa umaga, Ngayo'y uunos: sapagka't mapula at makulimlim ang langit. Kayo'y marurunong magsikilala ng anyo ng langit; datapuwa't hindi ninyo mangakilala ang mga tanda ng mga panahon.
            ["ailm"] = 40016004, // Mateo 16:4 — Ang isang lahing masama at mapangalunya ay humahanap ng tanda; at hindi siya bibigyan ng anomang tanda, kundi ng tanda ni Jonas. At sila'y iniwan niya, at yumaon.
            ["nypt"] = 40016010, // Mateo 16:10 — Ni yaong pitong tinapay sa apat na libong lalake, at kung ilang bakol ang inyong nailigpit?
            ["ahnn"] = 40016011, // Mateo 16:11 — Ano't hindi ninyo napaguunawa na hindi ang sinabi ko sa inyo'y tungkol sa tinapay? Datapuwa't kayo'y mangagingat sa lebadura ng mga Fariseo at ng mga Saduceo.
            ["ndns"] = 40016013, // Mateo 16:13 — Nang dumating nga si Jesus sa mga sakop ng Cesarea ni Filipo, ay itinanong niya sa kaniyang mga alagad, na sinasabi, Ano baga ang sabi ng mga tao kung sino ang Anak ng tao?
            ["aksa"] = 40016014, // Mateo 16:14 — At kanilang sinabi, Anang ilan, Si Juan Bautista; ang ilan, Si Elias; at ang mga iba, Si Jeremias, o isa sa mga propeta.
            ["kssk"] = 40016015, // Mateo 16:15 — Kaniyang sinabi sa kanila, Datapuwa't, ano ang sabi ninyo kung sino ako?
            ["iksi"] = 40016019, // Mateo 16:19 — Ibibigay ko sa iyo ang mga susi ng kaharian ng langit: at anomang iyong talian sa lupa ay tatalian sa langit; at anomang iyong kalagan sa lupa ay kakalagan sa langit.
            ["mnpy"] = 40016021, // Mateo 16:21 — Mula ng panahong yao'y nagpasimulang ipinakilala ni Jesus sa kaniyang mga alagad, na kinakailangang siya'y pumaroon sa Jerusalem, at magbata ng maraming bagay sa matatanda at sa mga pangulong saserdote at sa mga eskriba, at siya'y patayin, at muling ibangon sa ikatlong araw.
            ["aisn"] = 40016022, // Mateo 16:22 — At isinama siya ni Pedro, at nagpasimulang siya'y pinagwikaan, na nagsasabi, Panginoon, malayo ito sa iyo: kailan man ay hindi mangyayari ito sa iyo.
            ["sasm"] = 40016025, // Mateo 16:25 — Sapagka't ang sinomang magibig iligtas ang kaniyang buhay ay mawawalan nito: at ang sinomang mawalan ng kaniyang buhay dahil sa akin ay makakasumpong niyaon.
            ["anns"] = 40017003, // Mateo 17:3 — At narito, napakita sa kanila si Moises at si Elias na nakikipagusap sa kaniya.
            ["snps"] = 40017005, // Mateo 17:5 — Samantalang nagsasalita pa siya, narito, ang isang maningning na alapaap ay lumilim sa kanila: at narito, ang isang tinig na mula sa alapaap, na nagsasabi, Ito ang sinisinta kong Anak, na siya kong kinalulugdan; siya ang inyong pakinggan.
            ["ahsn"] = 40017009, // Mateo 17:9 — At habang sila'y nagsisibaba mula sa bundok, ay iniutos sa kanila ni Jesus, na nagsasabi, Huwag ninyong sabihin kanino mang tao ang pangitain, hanggang sa ang Anak ng tao ay ibangon sa mga patay.
            ["atsn"] = 40017010, // Mateo 17:10 — At tinanong siya ng kaniyang mga alagad, na nangagsasabi, Bakit nga sinasabi ng mga eskriba na kinakailangang pumarito muna si Elias?
            ["pmks"] = 40017015, // Mateo 17:15 — Panginoon, mahabag ka sa aking anak na lalake: sapagka't siya'y himatayin, at lubhang naghihirap; sapagka't madalas na siya'y nagsusugba sa apoy, at madalas sa tubig.
            ["asdk"] = 40017016, // Mateo 17:16 — At siya'y dinala ko sa iyong mga alagad, at hindi nila siya mapagaling.
            ["dagh"] = 40017021, // Mateo 17:21 — Datapuwa't ang ganito'y hindi lumalabas kundi sa pamamagitan ng panalangin at ayuno.
            ["snoa"] = 40017025, // Mateo 17:25 — Sinabi niya, Oo. At nang pumasok siya sa bahay, ay pinangunahan siya ni Jesus, na nagsasabi, Anong akala mo, Simon? ang mga hari sa lupa, kanino baga sila nanganiningil ng kabayaran ng buwis? sa kanilang mga anak baga o sa nangaiiba?
            ["dbkt"] = 40017027, // Mateo 17:27 — Datapuwa't, baka katisuran tayo nila, ay pumaroon ka sa dagat, at ihulog mo ang kawil, at kunin mo ang unang isdang lumitaw; at pagka naibuka mo na ang kaniyang bibig, ay masusumpungan mo ang isang siklo: kunin mo, at ibigay mo sa kanila sa ganang akin at sa iyo.
            ["nony"] = 40018001, // Mateo 18:1 — Nang oras na yaon ay nagsilapit ang mga alagad kay Jesus, na nangagsasabi, Sino nga baga ang pinakadakila sa kaharian ng langit?
            ["snmn"] = 40018004, // Mateo 18:4 — Sinoman ngang magpakababa na gaya ng maliit na batang ito, ay siyang pinakadakila sa kaharian ng langit.
            ["dsmn"] = 40018006, // Mateo 18:6 — Datapuwa't sinomang magbigay ng ikatitisod sa isa sa maliliit na ito na nagsisisampalataya sa akin, ay may pakikinabangin pa siya kung bitinan ang kaniyang leeg ng isang malaking batong gilingan, at siya'y ilubog sa kalaliman ng dagat.
            ["innh"] = 40018010, // Mateo 18:10 — Ingatan ninyo na huwag ninyong pawalang halaga ang isa sa maliliit na ito: sapagka't sinasabi ko sa inyo, na ang kanilang mga anghel sa langit ay nangakakakitang palagi ng mukha ng aking Ama na nasa langit.
            ["aaan"] = 40018012, // Mateo 18:12 — Ano ang akala ninyo? kung ang isang tao ay may isang daang tupa, at maligaw ang isa sa mga yaon, hindi baga iiwan niya ang siyam na pu't siyam, at pasasa kabundukan, at hahanapin ang naligaw?
            ["akmm"] = 40018013, // Mateo 18:13 — At kung mangyaring masumpungan niya, ay katotohanang sinasabi ko sa inyo, na magagalak ng higit dahil dito kay sa siyam na pu't siyam na hindi nangaligaw.
            ["gdnh"] = 40018014, // Mateo 18:14 — Gayon din na hindi nga kalooban ng inyong Amang nasa langit, na ang isa sa maliliit na ito ay mapahamak.
            ["akml"] = 40018015, // Mateo 18:15 — At kung magkasala laban sa iyo ang kapatid mo, pumaroon ka, at ipakilala mo sa kaniya ang kaniyang kasalanan na ikaw at siyang magisa: kung ikaw ay pakinggan niya, ay nagwagi ka sa iyong kapatid.
            ["dkhk"] = 40018016, // Mateo 18:16 — Datapuwa't kung hindi ka niya pakinggan, ay magsama ka pa ng isa o dalawa, upang sa bibig ng dalawang saksi o tatlo ay mapagtibay ang bawa't salita.
            ["msks"] = 40018019, // Mateo 18:19 — Muling sinasabi ko sa inyo, na kung pagkasunduan ng dalawa sa inyo sa lupa ang nauukol sa anomang bagay na kanilang hihingin, ay gagawin sa kanila ng aking Ama na nasa langit.
            ["nmls"] = 40018021, // Mateo 18:21 — Nang magkagayo'y lumapit si Pedro at sinabi sa kaniya, Panginoon, makailang magkakasala ang aking kapatid laban sa akin na siya'y aking patatawarin? hanggang sa makapito?
            ["kakn"] = 40018023, // Mateo 18:23 — Kaya't ang kaharian ng langit ay tulad sa isang hari, na nagibig na makipagusap sa kaniyang mga alipin.
            ["dpws"] = 40018025, // Mateo 18:25 — Datapuwa't palibhasa'y wala siyang sukat ibayad, ipinagutos ng kaniyang panginoon na siya'y ipagbili, at ang kaniyang asawa't mga anak, at ang lahat niyang tinatangkilik, at nang makabayad.
            ["ddaa"] = 40018026, // Mateo 18:26 — Dahil dito ang alipin ay nagpatirapa at sumamba sa kaniya, na nagsasabi, Panginoon, pagtiisan mo ako, at pagbabayaran ko sa iyong lahat.
            ["dlaa"] = 40018028, // Mateo 18:28 — Datapuwa't lumabas ang aliping yaon, at nasumpungan ang isa sa mga kapuwa niya alipin, na sa kaniya'y may utang na isang daang denario: at kaniyang hinawakan siya, at sinakal niya, na sinasabi, Bayaran mo ang utang mo.
            ["asaa"] = 40018030, // Mateo 18:30 — At siya'y ayaw: at yumaon at siya'y ipinabilanggo hanggang sa magbayad siya ng utang.
            ["nmps"] = 40018032, // Mateo 18:32 — Nang magkagayo'y pinalapit siya ng kaniyang panginoon, at sa kaniya'y sinabi, Ikaw na aliping masama, ipinatawad ko sa iyo ang lahat ng utang na yaon, sapagka't ipinamanhik mo sa akin:
            ["hbdn"] = 40018033, // Mateo 18:33 — Hindi baga dapat na ikaw naman ay mahabag sa iyong kapuwa alipin, na gaya ko namang nahabag sa iyo?
            ["asdd"] = 40019005, // Mateo 19:5 — At sinabi, Dahil dito'y iiwan ng lalake ang kaniyang ama at ina, at makikisama sa kaniyang asawa; at ang dalawa ay magiging isang laman?
            ["knhn"] = 40019006, // Mateo 19:6 — Kaya nga hindi na sila dalawa, kundi isang laman. Ang pinapagsama nga ng Dios, ay huwag papaghiwalayin ng tao.
            ["amaa"] = 40019010, // Mateo 19:10 — Ang mga alagad ay nangagsasabi sa kaniya, Kung ganyan ang kalagayan ng lalake sa kaniyang asawa, ay hindi nararapat magasawa.
            ["smmb"] = 40019012, // Mateo 19:12 — Sapagka't may mga bating, na ipinanganak na gayon mula sa tiyan ng kanilang mga ina: at may mga bating, na ginagawang bating ng mga tao: at may mga bating, na nangagpapakabating sa kanilang sarili dahil sa kaharian ng langit. Ang makakatanggap nito, ay pabayaang tumanggap.
            ["nmad"] = 40019013, // Mateo 19:13 — Nang magkagayon ay dinala sa kaniya ang maliliit na bata, upang ipatong niya ang kaniyang mga kamay sa kanila, at ipanalangin: at sinaway sila ng mga alagad.
            ["dsnj"] = 40019014, // Mateo 19:14 — Datapuwa't sinabi ni Jesus, Pabayaan ninyo ang maliliit na bata, at huwag ninyong pagbawalan silang magsilapit sa akin: sapagka't sa mga ganito ang kaharian ng langit.
            ["aabm"] = 40019029, // Mateo 19:29 — At ang bawa't magiwan ng mga bahay, o mga kapatid na lalake, o mga kapatid na babae, o ama, o ina, o mga anak, o mga lupa, dahil sa aking pangalan, ay tatanggap ng tigisang daan, at magsisipagmana ng walang hanggang buhay.
            ["dmmu"] = 40019030, // Mateo 19:30 — Datapuwa't maraming mga una na mangahuhuli; at mga huli na mangauuna.
            ["asln"] = 40020003, // Mateo 20:3 — At siya'y lumabas nang malapit na ang ikatlong oras, at nakita ang mga iba sa pamilihan na nangakatayong walang ginagawa;
            ["lsmn"] = 40020005, // Mateo 20:5 — Lumabas siyang muli nang malapit na ang mga oras na ikaanim at ikasiyam, at gayon din ang ginawa.
            ["alsn"] = 40020006, // Mateo 20:6 — At lumabas siya nang malapit na ang ikalabingisang oras at nakasumpong siya ng mga iba na nangakatayo; at sinabi niya sa kanila, Bakit kayo'y nangakatayo rito sa buong maghapon na walang ginagawa?
            ["anda"] = 40020008, // Mateo 20:8 — At nang dumating ang hapon, sinabi ng panginoon ng ubasan sa kaniyang katiwala, Tawagin mo ang mga manggagawa, at bayaran mo sila ng kaupahan sa kanila, na mula sa mga huli hanggang sa mga una.
            ["ankt"] = 40020011, // Mateo 20:11 — At nang kanilang tanggapin ay nangagbulongbulong laban sa puno ng sangbahayan,
            ["nnil"] = 40020012, // Mateo 20:12 — Na nangagsasabi, Isa lamang oras ang ginugol nitong mga huli, sila'y ipinantay mo sa amin, na aming binata ang hirap sa maghapon at ang init na nakasusunog.
            ["kmag"] = 40020014, // Mateo 20:14 — Kunin mo ang ganang iyo, at humayo ka sa iyong lakad; ibig kong bigyan itong huli, nang gaya rin sa iyo.
            ["hbms"] = 40020015, // Mateo 20:15 — Hindi baga matuwid sa aking gawin ang ibig ko sa aking pag-aari? o masama ang mata mo, sapagka't ako'y mabuti?
            ["kamu"] = 40020016, // Mateo 20:16 — Kaya't ang mga una'y mangahuhuli, at ang mga huli ay mangauuna.
            ["susj"] = 40020017, // Mateo 20:17 — Samantalang umaahon si Jesus, ay bukod niyang isinama ang labingdalawang alagad, at sa daa'y sinabi niya sa kanila,
            ["nnts"] = 40020018, // Mateo 20:18 — Narito, nagsisiahon tayo sa Jerusalem; at ibibigay ang Anak ng tao sa mga pangulong saserdote at sa mga eskriba; at kanilang hahatulang siya'y patayin,
            ["aiss"] = 40020019, // Mateo 20:19 — At ibibigay siya sa mga Gentil upang siya'y kanilang alimurahin, at hampasin, at ipako sa krus: at sa ikatlong araw siya'y ibabangon.
            ["nssj"] = 40020022, // Mateo 20:22 — Nguni't sumagot si Jesus at sinabi, Hindi ninyo nalalaman ang inyong hinihingi. Mangyayari bagang inuman ninyo ang sarong malapit nang aking iinuman? Sa kaniya'y sinabi nila, Mangyayari.
            ["sihm"] = 40020026, // Mateo 20:26 — Sa inyo'y hindi magkakagayon: kundi ang sinomang magibig na dumakila sa inyo ay magiging lingkod ninyo;
            ["anad"] = 40020030, // Mateo 20:30 — At narito, ang dalawang lalaking bulag na nangakaupo sa tabi ng daan, pagkarinig nilang nagdaraan si Jesus, ay nangagsisigaw, na nagsisipagsabi, Panginoon, mahabag ka sa amin, ikaw na Anak ni David.
            ["atsj"] = 40020032, // Mateo 20:32 — At tumigil si Jesus, at sila'y tinawag, at sinabi, Ano ang ibig ninyong gawin ko sa inyo?
            ["asjs"] = 40020034, // Mateo 20:34 — At si Jesus, sa pagkahabag, ay hinipo ang kanilang mga mata, at pagdaka'y nagsitanggap sila ng kanilang paningin; at nagsisunod sa kaniya.
            ["nssk"] = 40021002, // Mateo 21:2 — Na sinasabi sa kanila, Magsiparoon kayo sa nayong nasa tapat ninyo, at pagdaka'y masusumpungan ninyo ang isang nakatali na babaing asno, na may kasamang isang batang asno: kalagin ninyo, at dalhin ninyo sa akin.
            ["nniu"] = 40021004, // Mateo 21:4 — Nangyari nga ito, upang matupad ang sinalita sa pamamagitan ng propeta, na nagsasabi,
            ["akda"] = 40021007, // Mateo 21:7 — At kanilang dinala ang babaing asno, at ang batang asno, at inilagay nila sa ibabaw ng mga ito ang kanilang mga damit; at dito siya'y sumakay.
            ["aisd"] = 40021008, // Mateo 21:8 — At inilalatag sa daan ng kalakhang bahagi ng karamihan ang kanilang mga damit; at ang mga iba'y nagsiputol ng mga sanga ng mga punong kahoy, at inilalatag sa daan.
            ["apsj"] = 40021012, // Mateo 21:12 — At pumasok si Jesus sa templo ng Dios, at itinaboy niya ang lahat na nangagbibili at nangamimili sa templo, at ginulo niya ang mga dulang ng mga mamamalit ng salapi, at ang mga upuan ng mga nagbibili ng mga kalapati;
            ["aski"] = 40021017, // Mateo 21:17 — At sila'y kaniyang iniwan, at pumaroon sa labas ng bayan sa Betania, at nakipanuluyan doon.
            ["punn"] = 40021018, // Mateo 21:18 — Pagka umaga nga nang siya'y bumabalik sa bayan, nagutom siya.
            ["apsi"] = 40021019, // Mateo 21:19 — At pagkakita sa isang puno ng igos sa tabi ng daan, ay kaniyang nilapitan, at walang nasumpungang anoman doon, kundi mga dahon lamang; at sinabi niya rito, Mula ngayo'y huwag kang magbunga kailan man. At pagdaka'y natuyo ang puno ng igos.
            ["abnj"] = 40021025, // Mateo 21:25 — Ang bautismo ni Juan, saan baga nagmula? sa langit o sa mga tao? At kanilang pinagkatuwiranan sa kanilang sarili, na nangagsasabi, Kung sabihin natin, Sa langit; sasabihin niya sa atin, Bakit nga hindi ninyo siya pinaniwalaan?
            ["dkss"] = 40021026, // Mateo 21:26 — Datapuwa't kung sasabihin, Sa mga tao; nangatatakot tayo sa karamihan; sapagka't kinikilala ng lahat na propeta si Juan.
            ["dasa"] = 40021028, // Mateo 21:28 — Datapuwa't ano sa akala ninyo? Isang taong may dalawang anak; at lumapit siya sa una, at sinabi, Anak, pumaroon at gumawa ka ngayon sa ubasan.
            ["asls"] = 40021030, // Mateo 21:30 — At siya'y lumapit sa ikalawa, at gayon din ang sinabi. At sumagot siya at sinabi, Ginoo, ako'y paroroon: at hindi naparoon.
            ["absd"] = 40021031, // Mateo 21:31 — Alin baga sa dalawa ang gumanap ng kalooban ng kaniyang ama? Sinabi nila, Ang una. Sinabi sa kanila ni Jesus, Katotohanang sinasabi ko sa inyo, na ang mga maniningil ng buwis at ang mga patutot ay nangauuna sa inyo ng pagpasok sa kaharian ng Dios.
            ["pnai"] = 40021033, // Mateo 21:33 — Pakinggan ninyo ang isa pang talinghaga: May isang tao, na puno ng sangbahayan, na nagtanim ng isang ubasan, at binakuran niya ng mga buhay na punong kahoy sa palibot, at humukay roon ng isang pisaan ng ubas, at nagtayo ng isang bantayan, at ipinagkatiwala yaon sa mga magsasaka, at napasa ibang lupain.
            ["msna"] = 40021036, // Mateo 21:36 — Muling sinugo niya ang ibang mga alipin, na mahigit pa sa nangauna; at ginawa rin sa kanila ang gayon ding paraan.
            ["dpas"] = 40021037, // Mateo 21:37 — Datapuwa't pagkatapos ay sinugo niya sa kanila ang kaniyang anak na lalake, na nagsasabi, Igagalang nila ang aking anak.
            ["asak"] = 40022003, // Mateo 22:3 — At sinugo ang kaniyang mga alipin upang tawagin ang mga inanyayahan sa piging ng kasalan: at sila'y ayaw magsidalo.
            ["dhnp"] = 40022005, // Mateo 22:5 — Datapuwa't hindi nila pinansin, at sila'y nagsiyaon sa kanilang lakad, ang isa'y sa kaniyang sariling bukid, ang isa'y sa kaniyang mga kalakal;
            ["daha"] = 40022007, // Mateo 22:7 — Datapuwa't ang hari ay nagalit; at sinugo ang kaniyang mga hukbo, at pinuksa ang mga mamamataytaong yaon, at sinunog ang kanilang bayan.
            ["dpnh"] = 40022011, // Mateo 22:11 — Datapuwa't pagpasok ng hari upang tingnan ang mga panauhin, ay doo'y nakita niya ang isang tao na hindi nararamtan ng damit-kasalan:
            ["smam"] = 40022014, // Mateo 22:14 — Sapagka't marami ang mga tinawag, datapuwa't kakaunti ang mga nahirang.
            ["smns"] = 40022017, // Mateo 22:17 — Sabihin mo nga sa amin, Ano sa akala mo? Matuwid bagang bumuwis kay Cesar, o hindi?
            ["dnnj"] = 40022018, // Mateo 22:18 — Datapuwa't napagkikilala ni Jesus ang kanilang kasamaan, at sinabi sa kanila, Bakit ninyo ako tinutukso, kayong mga mapagpaimbabaw?
            ["insa"] = 40022019, // Mateo 22:19 — Ipakita ninyo sa akin ang salaping pangbuwis. At dinala nila sa kaniya ang isang denario.
            ["nsgs"] = 40022024, // Mateo 22:24 — Na sinasabi, Guro, sinabi ni Moises, Kung mamatay na walang mga anak ang isang lalake, ay magasawa ang kaniyang kapatid na lalake sa asawa niya, at magkakaanak sa kaniyang kapatid na lalake.
            ["spnm"] = 40022028, // Mateo 22:28 — Sa pagkabuhay ngang maguli sino kaya doon sa pito ang magiging asawa? sapagka't siya'y naging asawa nilang lahat.
            ["dtsp"] = 40022031, // Mateo 22:31 — Datapuwa't tungkol sa pagkabuhay na maguli ng mga patay, hindi baga ninyo nabasa ang sinalita sa inyo ng Dios, na nagsasabi,
            ["aisk"] = 40022035, // Mateo 22:35 — At isa sa kanila, na tagapagtanggol ng kautusan, ay tinanong siya ng isang tanong, upang siya'y tuksuhin:
            ["gaba"] = 40022036, // Mateo 22:36 — Guro, alin baga ang dakilang utos sa kautusan?
            ["sdun"] = 40022040, // Mateo 22:40 — Sa dalawang utos na ito'y nauuwi ang buong kautusan, at ang mga propeta.
            ["hnna"] = 40022041, // Mateo 22:41 — Habang nangagkakatipon nga ang mga Fariseo, ay tinanong sila ni Jesus ng isang tanong.
            ["nsaa"] = 40022042, // Mateo 22:42 — Na sinasabi, Ano ang akala ninyo tungkol kay Cristo? kanino bagang anak siya? Sinabi nila sa kaniya, kay David.
            ["ktns"] = 40022045, // Mateo 22:45 — Kung tinatawag nga siya ni David na Panginoon, paanong siya'y kaniyang anak?
            ["awsn"] = 40022046, // Mateo 22:46 — At wala sinomang nakasagot sa kaniya ng isang salita, ni wala sinomang nangahas buhat sa araw na yaon na tumanong pa sa kaniya ng anomang mga tanong.
            ["nnna"] = 40023002, // Mateo 23:2 — Na nagsasabi, Nagsisiupo ang mga eskriba at mga Fariseo sa luklukan ni Moises.
            ["lnnm"] = 40023003, // Mateo 23:3 — Lahat nga ng mga bagay na sa inyo'y kanilang ipagutos, ay gawin ninyo at ganapin: datapuwa't huwag kayong magsigawa ng alinsunod sa kanilang mga gawa; sapagka't kanilang sinasabi, at hindi ginagawa.
            ["osnn"] = 40023004, // Mateo 23:4 — Oo, sila'y nangagbibigkis ng mabibigat na pasan at mahihirap na dalhin, at ipinapasan nila sa mga balikat ng mga tao; datapuwa't ayaw man lamang nilang kilusin ng kanilang daliri.
            ["dgna"] = 40023005, // Mateo 23:5 — Datapuwa't ginagawa nila ang lahat ng kanilang mga gawa upang mangakita ng mga tao: sapagka't nangagpapalapad sila ng kanilang mga pilakteria, at nangagpapalapad ng mga laylayan ng kanilang mga damit,
            ["aiam"] = 40023006, // Mateo 23:6 — At iniibig ang mga pangulong dako sa mga pigingan, at ang mga pangulong luklukan sa mga sinagoga,
            ["dkhp"] = 40023008, // Mateo 23:8 — Datapuwa't kayo'y huwag patawag na Rabi: sapagka't iisa ang inyong guro, at kayong lahat ay magkakapatid.
            ["ahnt"] = 40023009, // Mateo 23:9 — At huwag ninyong tawaging inyong ama ang sinomang tao sa lupa: sapagka't iisa ang inyong ama, sa makatuwid baga'y siya na nasa langit.
            ["nhkp"] = 40023010, // Mateo 23:10 — Ni huwag kayong patawag na mga panginoon; sapagka't iisa ang inyong panginoon, sa makatuwid baga'y ang Cristo.
            ["daps"] = 40023011, // Mateo 23:11 — Datapuwa't ang pinakadakila sa inyo ay magiging lingkod ninyo.
            ["sank"] = 40023016, // Mateo 23:16 — Sa aba ninyo, kayong mga tagaakay na bulag, na inyong sinasabi, Kung ipanumpa ninoman ang templo, ay walang anoman; datapuwa't kung ipanumpa ninoman ang ginto ng templo, ay nagkakautang nga siya.
            ["kmbs"] = 40023019, // Mateo 23:19 — Kayong mga bulag: sapagka't alin baga ang lalong dakila, ang handog, o ang dambana na bumabanal sa handog?
            ["kans"] = 40023020, // Mateo 23:20 — Kaya't ang nanunumpa sa pamamagitan ng dambana, ay ipinanunumpa ito, at ang lahat ng mga bagay na nangasa ibabaw nito.
            ["ansp"] = 40023022, // Mateo 23:22 — Ang nanunumpa sa pamamagitan ng langit, ay ipinanumpa ang luklukan ng Dios, at yaong nakaluklok doon.
            ["ibnf"] = 40023026, // Mateo 23:26 — Ikaw bulag na Fariseo, linisin mo muna ang loob ng saro at ng pinggan, upang luminis naman ang kaniyang labas.
            ["gdnk"] = 40023028, // Mateo 23:28 — Gayon din naman kayo, sa labas ay nangagaanyong matuwid sa mga tao, datapuwa't sa loob ay puno kayo ng pagpapaimbabaw at ng katampalasanan.
            ["kkns"] = 40023031, // Mateo 23:31 — Kaya't kayo'y nangagpapatotoo sa inyong sarili, na kayo'y mga anak niyaong mga nagsipatay ng mga propeta.
            ["umsi"] = 40023035, // Mateo 23:35 — Upang mabubo sa inyo ang lahat na matuwid na dugo na nabuhos sa ibabaw ng lupa, buhat sa dugo ng matuwid na si Abel hanggang sa dugo ni Zacarias na anak ni Baraquias na pinatay ninyo sa pagitan ng santuario at ng dambana.
            ["ojjn"] = 40023037, // Mateo 23:37 — Oh Jerusalem, Jerusalem, na pumapatay sa mga propeta, at bumabato sa mga sinusugo sa kaniya! makailang inibig kong tipunin ang iyong mga anak, na gaya ng pagtitipon ng inahing manok sa kaniyang mga sisiw sa ilalim ng kaniyang mga pakpak, ay ayaw kayo!
            ["naib"] = 40023038, // Mateo 23:38 — Narito, ang inyong bahay ay iniiwan sa inyong wasak.
            ["amkn"] = 40024006, // Mateo 24:6 — At mangakakarinig kayo ng mga digmaan at mga alingawngaw ng mga digmaan; ingatan ninyo na huwag kayong magulumihanan: sapagka't kinakailangang ito'y mangyari datapuwa't hindi pa ang wakas.
            ["smab"] = 40024007, // Mateo 24:7 — Sapagka't magsisitindig ang bansa laban sa bansa, at ang kaharian laban sa kaharian; at magkakagutom at lilindol sa iba't ibang dako.
            ["daln"] = 40024008, // Mateo 24:8 — Datapuwa't ang lahat ng mga bagay na ito ay siyang pasimula ng kahirapan.
            ["kmik"] = 40024009, // Mateo 24:9 — Kung magkagayo'y ibibigay kayo sa kapighatian, at kayo'y papatayin: at kayo'y kapopootan ng lahat ng mga bansa dahil sa aking pangalan.
            ["amam"] = 40024011, // Mateo 24:11 — At magsisibangon ang maraming bulaang propeta, at kanilang ililigaw ang marami.
            ["adsp"] = 40024012, // Mateo 24:12 — At dahil sa pagsagana ng katampalasanan, ang pagibig ng marami ay lalamig.
            ["damh"] = 40024013, // Mateo 24:13 — Datapuwa't ang magtitiis hanggang sa wakas ay siyang maliligtas.
            ["aiae"] = 40024014, // Mateo 24:14 — At ipangangaral ang evangeliong ito ng kaharian sa buong sanglibutan sa pagpapatotoo sa lahat ng mga bansa; at kung magkagayo'y darating ang wakas.
            ["knpn"] = 40024015, // Mateo 24:15 — Kaya nga pagkakita ninyo ng kasuklamsuklam na paninira, na sinalita sa pamamagitan ng propeta Daniel, na natatayo sa dakong banal (unawain ng bumabasa),
            ["kmms"] = 40024016, // Mateo 24:16 — Kung magkagayo'y magsitakas sa mga bundok ang nangasa Judea:
            ["aanb"] = 40024018, // Mateo 24:18 — At ang nasa bukid ay huwag magbalik upang kumuha ng kaniyang balabal.
            ["skmm"] = 40024021, // Mateo 24:21 — Sapagka't kung magkagayo'y magkakaroon ng malaking kapighatian, na ang gayo'y di pa nangyayari buhat sa pasimula ng sanglibutan hanggang ngayon, at ni hindi na mangyayari kailan man.
            ["ampa"] = 40024022, // Mateo 24:22 — At malibang paikliin ang mga araw na yaon, ay walang lamang makaliligtas: datapuwa't dahil sa mga hirang ay paiikliin ang mga araw na yaon.
            ["kmkm"] = 40024023, // Mateo 24:23 — Kung magkagayon, kung may magsabi sa inyong sinomang tao, Narito ang Cristo, o, Nariyan; huwag ninyong paniwalaan.
            ["smmn"] = 40024024, // Mateo 24:24 — Sapagka't may magsisilitaw na mga bulaang Cristo, at mga bulaang propeta, at mangagpapakita ng mga dakilang tanda at mga kababalaghan; ano pa't ililigaw, kung maaari, pati ng mga hirang.
            ["knks"] = 40024026, // Mateo 24:26 — Kaya nga kung sa inyo'y kanilang sasabihin, Narito, siya'y nasa ilang; huwag kayong magsilabas: Narito, siya'y nasa mga silid; huwag ninyong paniwalaan.
            ["sgnk"] = 40024027, // Mateo 24:27 — Sapagka't gaya ng kidlat na kumikidlat sa silanganan, at nakikita hanggang sa kalunuran; gayon din naman ang pagparito ng Anak ng tao.
        };


        private static readonly Dictionary<int, string> ReverseSCommands = SCommands.ToDictionary(x => x.Value, x => x.Key);

        public string AliasForThisVerse(int encodedBookValue)
        {
            if (ReverseSCommands.TryGetValue(encodedBookValue, out string alias))
            {
                string result = "S-state command: " + alias;
                return result;
            }
            else
            {
                return "S-state command: No alias found";
            }
        }

        
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
                commandString = "/" + searchQuery;  
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
                    commandString = "/" + searchQuery;
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
                    commandString = "/" + searchQuery;
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
            commandString = "/" + searchQuery;
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
