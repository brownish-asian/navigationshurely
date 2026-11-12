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
        private int previousQuantifier = 1;
        private char? activeFilter;
        private string searchQuery = string.Empty;
        private string wholeQueryWithCommas;
        private string commandString = string.Empty;
        private bool normalChoosingMode = true;
        private bool justANormalStateChange = false;

        public event Action<bool>? SearchUiVisibilityChanged;
        public event Action<bool>? ShowMarks;
        public event Action<bool>? HideTheEnterBox;

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
            if (justANormalStateChange && state != State.Query && state != State.Choice) 
            {
                return "";
            }
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
                case '\u0003':
                    commandString += "Shift+[Tab]";
                    break;
                case '\u0001':
                    commandString += "Ctrl+F";
                    break;
                case '\u0002':
                    commandString += "Ctrl+B";
                    break;
                default:
                    commandString += k;
                    break;
            }
        }

        public Color CommandColor(bool classic)
        {
            switch (validity)
            {
                case EntireCommandStatus.Incomplete:
                    return Color.FromRgb(85, 200, 208);
                case EntireCommandStatus.Invalid:
                    return Color.FromRgb(255, 107, 120);
                case EntireCommandStatus.Valid:
                    bool wasNormalChange = justANormalStateChange;
                    justANormalStateChange = false;

                    if (wasNormalChange && !classic)
                    {
                        return Color.FromArgb(0x55, 0x07, 0x13, 0x20);
                    }
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


        private static readonly Dictionary<char, int> FastQuantifiers = new()
        {
            ['h'] = 10,
            ['l'] = 11,
            ['w'] = 12,
            ['f'] = 15,
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


        // Aliases derived from Ang Dating Biblia (Tagalog).
        // Each key is the first letter of the verse's first four space-separated words.
        // Values encode BookId * 1,000,000 + Chapter * 1,000 + Verse.
        private static readonly Dictionary<string, int> SCommands = new()
        {
            ["npan"] = 1001001,
            ["aala"] = 1001002,
            ["asnd"] = 1001003,
            ["atnd"] = 1001005,
            ["agnd"] = 1001007,
            ["annd"] = 1001027,
            ["anal"] = 1002001,
            ["ania"] = 1002002,
            ["abnd"] = 1002003,
            ["iapn"] = 1002004,
            ["annp"] = 1002007,
            ["aana"] = 1003001,
            ["asnb"] = 1003002,
            ["dsbn"] = 1003003,
            ["asna"] = 1003004,
            ["anmn"] = 1003006,
            ["snnp"] = 1012001,
            ["agki"] = 1012002,
            ["apka"] = 1012003,
            ["aink"] = 1050016,
            ["gsnk"] = 1050017,
            ["anak"] = 1050018,
            ["atsi"] = 1050020,
            ["knhk"] = 1050021,
            ["aapa"] = 2014001,
            ["smsm"] = 2014002,
            ["asnf"] = 2014003,
            ["ansh"] = 2014005,
            ["ainf"] = 2014006,
            ["iknp"] = 2014014,
            ["aapm"] = 2020002,
            ["hkmn"] = 2020003,
            ["hkgp"] = 2020004,
            ["hmys"] = 2020005,
            ["apkn"] = 2020006,
            ["hmba"] = 2020007,
            ["imai"] = 2020012,
            ["hkmo"] = 3019018,
            ["pknn"] = 4006024,
            ["pnnp"] = 4006025,
            ["innp"] = 4006026,
            ["inau"] = 5006001,
            ["niam"] = 5006002,
            ["dmno"] = 5006003,
            ["dmoi"] = 5006004,
            ["aiia"] = 5006005,
            ["aams"] = 5006006,
            ["kmam"] = 5031006,
            ["nnpn"] = 6001001,
            ["smna"] = 6001002,
            ["msia"] = 6001004,
            ["wmst"] = 6001005,
            ["mkam"] = 6001006,
            ["aani"] = 6001008,
            ["hbki"] = 6001009,
            ["akka"] = 6024003,
            ["aikk"] = 6024004,
            ["aass"] = 6024005,
            ["aika"] = 6024006,
            ["ansd"] = 6024007,
            ["akin"] = 6024015,
            ["asnr"] = 8001016,
            ["nsnp"] = 9016007,
            ["kaab"] = 14007014,
            ["asnh"] = 18001021,
            ["nknm"] = 18042002,
            ["matn"] = 19001001,
            ["apaa"] = 19023001,
            ["kpas"] = 19023002,
            ["kpaa"] = 19023003,
            ["obal"] = 19023004,
            ["ipan"] = 19023005,
            ["tnak"] = 19023006,
            ["ndsa"] = 19027002,
            ["ibah"] = 19027004,
            ["dmop"] = 19027007,
            ["nish"] = 19027008,
            ["apap"] = 19034001,
            ["aaka"] = 19034002,
            ["odnn"] = 19034003,
            ["ahap"] = 19034004,
            ["oita"] = 19034008,
            ["apam"] = 19034018,
            ["hkmd"] = 19037001,
            ["ssmp"] = 19037002,
            ["mkns"] = 19037004,
            ["akpg"] = 19037006,
            ["mknp"] = 19037008,
            ["samm"] = 19037009,
            ["alnt"] = 19037023,
            ["adaa"] = 19046001,
            ["khtm"] = 19046002,
            ["miin"] = 19046004,
            ["adan"] = 19046005,
            ["amba"] = 19046006,
            ["apnm"] = 19046007,
            ["kama"] = 19046010,
            ["mksa"] = 19051001,
            ["hmal"] = 19051002,
            ["skka"] = 19051003,
            ["lsis"] = 19051004,
            ["nais"] = 19051005,
            ["lman"] = 19051010,
            ["spam"] = 19056003,
            ["stsl"] = 19091001,
            ["asts"] = 19091002,
            ["skik"] = 19091003,
            ["ktkn"] = 19091004,
            ["iahm"] = 19091005,
            ["dssn"] = 19091006,
            ["ilam"] = 19091007,
            ["imln"] = 19091008,
            ["siop"] = 19091009,
            ["wkms"] = 19091010,
            ["kdks"] = 19091012,
            ["iyal"] = 19091013,
            ["stsa"] = 19091015,
            ["absn"] = 19091016,
            ["anna"] = 19100003,
            ["sapa"] = 19100005,
            ["pmap"] = 19103001,
            ["nsnn"] = 19103003,
            ["nstn"] = 19103004,
            ["nsbs"] = 19103005,
            ["kiak"] = 19103007,
            ["shgs"] = 19103010,
            ["kgal"] = 19103012,
            ["iaan"] = 19118024,
            ["msss"] = 19119001,
            ["msnn"] = 19119002,
            ["oshn"] = 19119003,
            ["iisa"] = 19119004,
            ["omna"] = 19119005,
            ["spln"] = 19119009,
            ["asma"] = 19119011,
            ["asmi"] = 19119105,
            ["ikaa"] = 19121001,
            ["assa"] = 19121002,
            ["apat"] = 19121005,
            ["hksn"] = 19121006,
            ["minp"] = 19127001,
            ["mnnp"] = 19133001,
            ["opis"] = 19139001,
            ["inaa"] = 19139002,
            ["isaa"] = 19139003,
            ["swpa"] = 19139004,
            ["ikas"] = 19139005,
            ["amsi"] = 19139014,
            ["pnbb"] = 19150006,
            ["sata"] = 20001024,
            ["akhm"] = 20003001,
            ["sknm"] = 20003002,
            ["hkpn"] = 20003003,
            ["tksp"] = 20003005,
            ["kmss"] = 20003006,
            ["mksi"] = 20003008,
            ["apsp"] = 20009010,
            ["apah"] = 20010012,
            ["ahnm"] = 20015008,
            ["ampn"] = 20016001,
            ["imsp"] = 20016003,
            ["gnpa"] = 20016004,
            ["spnk"] = 20016006,
            ["paml"] = 20016007,
            ["apnt"] = 20016009,
            ["agnt"] = 20016011,
            ["akan"] = 20016018,
            ["mmsa"] = 20016024,
            ["akau"] = 20017017,
            ["ahah"] = 20018001,
            ["amaw"] = 20018002,
            ["pama"] = 20018003,
            ["amsn"] = 20018004,
            ["amln"] = 20018006,
            ["apnp"] = 20018010,
            ["kaba"] = 20018021,
            ["mmks"] = 20019021,
            ["aksk"] = 20022004,
            ["tmab"] = 20022006,
            ["aban"] = 20027017,
            ["imnb"] = 20031010,
            ["sbba"] = 21003001,
            ["iawn"] = 21012013,
            ["anka"] = 23006008,
            ["ssaa"] = 23009006,
            ["iiss"] = 23026003,
            ["amnk"] = 23033006,
            ["iaia"] = 23040001,
            ["mkmp"] = 23040002,
            ["atni"] = 23040003,
            ["oinn"] = 23040009,
            ["napd"] = 23040010,
            ["nsns"] = 23040031,
            ["mksh"] = 23041001,
            ["snni"] = 23041002,
            ["khsa"] = 23041003,
            ["sgay"] = 23041004,
            ["nnmp"] = 23041005,
            ["hkms"] = 23041010,
            ["nnga"] = 23043001,
            ["piad"] = 23043002,
            ["assh"] = 23043006,
            ["iiab"] = 23043008,
            ["snsa"] = 23053001,
            ["shai"] = 23053003,
            ["nsnd"] = 23053005,
            ["tlng"] = 23053006,
            ["sngm"] = 23053007,
            ["agna"] = 23053009,
            ["wang"] = 23054017,
            ["saam"] = 23055008,
            ["inam"] = 24029001,
            ["pnms"] = 24029002,
            ["aiha"] = 24029007,
            ["sgas"] = 24029008,
            ["snka"] = 24029011,
            ["akms"] = 24029012,
            ["amsk"] = 24030019,
            ["spys"] = 24031001,
            ["apan"] = 24031003,
            ["mika"] = 24031004,
            ["mkmn"] = 24031005,
            ["nads"] = 24031008,
            ["tksa"] = 24033003,
            ["smkn"] = 25003022,
            ["amyb"] = 25003023,
            ["naad"] = 27003017,
            ["kisi"] = 33006008,
            ["apmk"] = 34001007,
            ["nakk"] = 35002004,
            ["sbap"] = 35003017,
            ["apmd"] = 36003017,
            ["dnab"] = 39003010,
            ["nadm"] = 40001023,
            ["apps"] = 40002012,
            ["adhs"] = 40002015,
            ["skab"] = 40003011,
            ["dssa"] = 40004004,
            ["mamm"] = 40005003,
            ["mans"] = 40005004,
            ["mams"] = 40005005,
            ["mana"] = 40005006,
            ["mamp"] = 40005010,
            ["mkpk"] = 40005011,
            ["kain"] = 40005014,
            ["lnga"] = 40005016,
            ["kkim"] = 40005023,
            ["dsks"] = 40005044,
            ["knpi"] = 40006002,
            ["dpia"] = 40006003,
            ["dipi"] = 40006006,
            ["mnkn"] = 40006009,
            ["dnak"] = 40006010,
            ["aims"] = 40006012,
            ["ahmk"] = 40006013,
            ["skin"] = 40006014,
            ["dkhn"] = 40006015,
            ["kmkn"] = 40006020,
            ["sksn"] = 40006021,
            ["mnam"] = 40006026,
            ["nkpn"] = 40006030,
            ["khkm"] = 40006031,
            ["dhmn"] = 40006033,
            ["khni"] = 40006034,
            ["hkmu"] = 40007001,
            ["mkak"] = 40007007,
            ["sabh"] = 40007008,
            ["oats"] = 40007009,
            ["knln"] = 40007012,
            ["kmsm"] = 40007013,
            ["skmb"] = 40007016,
            ["habn"] = 40007021,
            ["kabd"] = 40007024,
            ["aabd"] = 40007026,
            ["anmi"] = 40008010,
            ["dumn"] = 40009006,
            ["dpnj"] = 40009022,
            ["alis"] = 40010005,
            ["akkn"] = 40010022,
            ["dpkp"] = 40010023,
            ["kabk"] = 40010032,
            ["msak"] = 40011028,
            ["pnaa"] = 40011029,
            ["ssim"] = 40012037,
            ["ashg"] = 40013058,
            ["nmsn"] = 40016024,
            ["snmn"] = 40018004,
            ["innh"] = 40018010,
            ["gdnh"] = 40018014,
            ["msks"] = 40018019,
            ["dpws"] = 40018025,
            ["ashn"] = 40018027,
            ["asdd"] = 40019005,
            ["dsnj"] = 40019014,
            ["anls"] = 40019016,
            ["apnj"] = 40019026,
            ["kamu"] = 40020016,
            ["sihm"] = 40020026,
            ["assj"] = 40021021,
            ["alnm"] = 40021022,
            ["absd"] = 40021031,
            ["smam"] = 40022014,
            ["assk"] = 40022037,
            ["iada"] = 40022038,
            ["aapk"] = 40022039,
            ["dkhp"] = 40023008,
            ["nhkp"] = 40023010,
            ["adsp"] = 40024012,
            ["damh"] = 40024013,
            ["aiae"] = 40024014,
            ["dkpn"] = 40024029,
            ["spni"] = 40024032,
            ["alaa"] = 40024035,
            ["ntsa"] = 40024036,
            ["akpa"] = 40024037,
            ["kmsb"] = 40024040,
            ["dbns"] = 40024041,
            ["ditn"] = 40024043,
            ["knkm"] = 40024044,
            ["dkam"] = 40024048,
            ["dama"] = 40025004,
            ["asah"] = 40025040,
            ["dhus"] = 40026063,
            ["asaa"] = 40028005,
            ["swrs"] = 40028006,
            ["amkm"] = 40028007,
            ["alsj"] = 40028018,
            ["ddmn"] = 40028019,
            ["nins"] = 40028020,
            ["apne"] = 41001001,
            ["aspk"] = 41001002,
            ["dsjn"] = 41001004,
            ["annm"] = 41001009,
            ["amit"] = 41001011,
            ["asnn"] = 41001015,
            ["apst"] = 41001016,
            ["apin"] = 41001018,
            ["alpa"] = 41001028,
            ["ankp"] = 41001032,
            ["dsua"] = 41001045,
            ["anim"] = 41002017,
            ["awtn"] = 41002022,
            ["kpps"] = 41002026,
            ["kaan"] = 41002028,
            ["ksks"] = 41003028,
            ["ssgn"] = 41003035,
            ["ukms"] = 41004012,
            ["agsa"] = 41004039,
            ["ahni"] = 41005019,
            ["asyn"] = 41005020,
            ["dhpn"] = 41005036,
            ["anss"] = 41006006,
            ["sshd"] = 41006017,
            ["alsa"] = 41006034,
            ["pmsu"] = 41006036,
            ["samf"] = 41007003,
            ["nnau"] = 41007008,
            ["wanl"] = 41007015,
            ["smsl"] = 41007021,
            ["sinm"] = 41008025,
            ["asss"] = 41009019,
            ["psaa"] = 41009024,
            ["dhsn"] = 41009027,
            ["maad"] = 41009050,
            ["dnim"] = 41010014,
            ["ansu"] = 41010017,
            ["nmam"] = 41010019,
            ["apsk"] = 41010021,
            ["dsns"] = 41010022,
            ["anam"] = 41010024,
            ["mpsi"] = 41010025,
            ["asnl"] = 41010026,
            ["pnjs"] = 41010027,
            ["snjk"] = 41010029,
            ["nhst"] = 41010030,
            ["dmna"] = 41010031,
            ["aska"] = 41010034,
            ["dsia"] = 41010043,
            ["saan"] = 41010045,
            ["aanu"] = 41011009,
            ["knsk"] = 41011024,
            ["akmk"] = 41011025,
            ["dkhk"] = 41011026,
            ["hsad"] = 41012027,
            ["aima"] = 41012030,
            ["apai"] = 41012031,
            ["aasi"] = 41012033,
            ["asja"] = 41013005,
            ["mpsa"] = 41013006,
            ["apkd"] = 41013011,
            ["dmkn"] = 41013023,
            ["kmma"] = 41013033,
            ["gnak"] = 41014008,
            ["aksa"] = 41014036,
            ["amsu"] = 41014039,
            ["alsb"] = 41014041,
            ["apbp"] = 41014072,
            ["apsn"] = 41016014,
            ["asns"] = 41016015,
            ["alam"] = 41016017,
            ["smnm"] = 41016018,
            ["apjn"] = 41016019,
            ["umma"] = 42001004,
            ["ssmd"] = 42001015,
            ["asls"] = 42001017,
            ["asnz"] = 42001018,
            ["apss"] = 42001028,
            ["smda"] = 42001032,
            ["swsm"] = 42001037,
            ["amab"] = 42001045,
            ["anaa"] = 42001047,
            ["snna"] = 42001048,
            ["nisa"] = 42001074,
            ["skak"] = 42001075,
            ["sisi"] = 42002011,
            ["lsds"] = 42002014,
            ["npmp"] = 42002029,
            ["alab"] = 42002040,
            ["amnl"] = 42003006,
            ["absk"] = 42003022,
            ["absj"] = 42004014,
            ["saae"] = 42004018,
            ["ands"] = 42004041,
            ["dsls"] = 42005016,
            ["aame"] = 42005021,
            ["hapu"] = 42005032,
            ["ssnt"] = 42006014,
            ["apnb"] = 42006019,
            ["aina"] = 42006020,
            ["mknn"] = 42006021,
            ["mkkk"] = 42006022,
            ["dsan"] = 42006024,
            ["sanm"] = 42006025,
            ["sanp"] = 42006026,
            ["pnas"] = 42006028,
            ["sssi"] = 42006029,
            ["akaa"] = 42006031,
            ["akkm"] = 42006032,
            ["dina"] = 42006035,
            ["mkgn"] = 42006036,
            ["swmp"] = 42006043,
            ["amta"] = 42006045,
            ["abtn"] = 42006046,
            ["abls"] = 42006047,
            ["aasm"] = 42008015,
            ["inkp"] = 42008018,
            ["uksi"] = 42008039,
            ["dnmi"] = 42008050,
            ["sasm"] = 42009024,
            ["dksk"] = 42009027,
            ["dhnn"] = 42009045,
            ["nbkk"] = 42010019,
            ["apns"] = 42010027,
            ["asks"] = 42011009,
            ["dsno"] = 42011028,
            ["mspa"] = 42011032,
            ["knka"] = 42011036,
            ["dmam"] = 42012007,
            ["saba"] = 42012023,
            ["gmhn"] = 42012031,
            ["myma"] = 42012037,
            ["dahn"] = 42012048,
            ["anum"] = 42012049,
            ["sspk"] = 42012058,
            ["shnn"] = 42014027,
            ["sksi"] = 42015007,
            ["gdsk"] = 42015010,
            ["dkma"] = 42015032,
            ["wams"] = 42016013,
            ["asnp"] = 42017006,
            ["mdln"] = 42018010,
            ["dpsn"] = 42018016,
            ["tmam"] = 42018020,
            ["dsna"] = 42018027,
            ["anns"] = 42019037,
            ["snhd"] = 42020038,
            ["sipa"] = 42021019,
            ["mamt"] = 42021026,
            ["asdn"] = 42022019,
            ["akss"] = 42022025,
            ["dsih"] = 42022026,
            ["diai"] = 42022032,
            ["nsak"] = 42022042,
            ["dpsk"] = 42023028,
            ["asnj"] = 42023034,
            ["dsai"] = 42023040,
            ["atsk"] = 42023041,
            ["asjn"] = 42023046,
            ["wsrd"] = 42024006,
            ["assn"] = 42024032,
            ["nmbn"] = 42024045,
            ["npsa"] = 43001001,
            ["daln"] = 43001012,
            ["anav"] = 43001014,
            ["psnj"] = 43001015,
            ["sskk"] = 43001016,
            ["sisp"] = 43001017,
            ["wtnk"] = 43001018,
            ["akia"] = 43001020,
            ["snns"] = 43001022,
            ["snaa"] = 43001023,
            ["assb"] = 43001024,
            ["ssnj"] = 43001026,
            ["snap"] = 43001027,
            ["nkan"] = 43001029,
            ["ashk"] = 43001031,
            ["ansj"] = 43001032,
            ["skdk"] = 43001042,
            ["snki"] = 43002005,
            ["iran"] = 43003002,
            ["ssja"] = 43003003,
            ["ssjk"] = 43003005,
            ["hahk"] = 43003008,
            ["uass"] = 43003015,
            ["sgnl"] = 43003016,
            ["shsn"] = 43003017,
            ["dagn"] = 43003021,
            ["skdn"] = 43003030,
            ["dasu"] = 43004014,
            ["ddao"] = 43004023,
            ["adae"] = 43004024,
            ["mktn"] = 43004029,
            ["auat"] = 43004036,
            ["ddnl"] = 43005018,
            ["skpi"] = 43005021,
            ["kksk"] = 43005024,
            ["hnii"] = 43005028,
            ["dnkk"] = 43005042,
            ["pkmk"] = 43005044,
            ["knnj"] = 43006011,
            ["mkhd"] = 43006027,
            ["alni"] = 43006037,
            ["siak"] = 43006040,
            ["aena"] = 43006063,
            ["kast"] = 43007017,
            ["nhan"] = 43007037,
            ["asnw"] = 43008011,
            ["mnns"] = 43008012,
            ["snnj"] = 43008031,
            ["kpnk"] = 43008036,
            ["dnpn"] = 43008040,
            ["asda"] = 43008047,
            ["ssal"] = 43009011,
            ["ddtn"] = 43009024,
            ["nnhp"] = 43009031,
            ["nnjn"] = 43009035,
            ["snjs"] = 43010006,
            ["hpam"] = 43010010,
            ["aamp"] = 43010011,
            ["gnps"] = 43010015,
            ["dnam"] = 43010027,
            ["asbk"] = 43010028,
            ["ktnm"] = 43010035,
            ["dkgk"] = 43010038,
            ["sskn"] = 43011025,
            ["aasn"] = 43011026,
            ["anni"] = 43012046,
            ["ankn"] = 43012050,
            ["tnag"] = 43013013,
            ["skbk"] = 43013015,
            ["nmgn"] = 43013021,
            ["ibua"] = 43013034,
            ["sgmn"] = 43013035,
            ["hmai"] = 43014001,
            ["akap"] = 43014003,
            ["aaai"] = 43014013,
            ["kkmn"] = 43014014,
            ["kaii"] = 43014015,
            ["hkki"] = 43014018,
            ["kppa"] = 43014019,
            ["akai"] = 43014027,
            ["kmns"] = 43015003,
            ["kmsa"] = 43015004,
            ["aapn"] = 43015005,
            ["kkms"] = 43015007,
            ["sgla"] = 43015008,
            ["kpia"] = 43015009,
            ["iaau"] = 43015012,
            ["wmld"] = 43015013,
            ["ahnh"] = 43015016,
            ["kkts"] = 43015019,
            ["kash"] = 43015024,
            ["gmsk"] = 43016007,
            ["gmks"] = 43016013,
            ["hnwp"] = 43016024,
            ["ambn"] = 43016033,
            ["gnim"] = 43017002,
            ["aiab"] = 43017003,
            ["pmss"] = 43017017,
            ["hlsa"] = 43017020,
            ["usla"] = 43017021,
            ["snsk"] = 43018037,
            ["ddpn"] = 43019012,
            ["nmnn"] = 43019030,
            ["snms"] = 43020021,
            ["nami"] = 43020031,
            ["insn"] = 43021019,
            ["amdi"] = 43021025,
            ["dtna"] = 44001008,
            ["nnnk"] = 44001011,
            ["anma"] = 44001015,
            ["mkkm"] = 44001016,
            ["ssis"] = 44001017,
            ["knat"] = 44001018,
            ["ains"] = 44001019,
            ["smti"] = 44001021,
            ["mspn"] = 44001022,
            ["utak"] = 44001025,
            ["andn"] = 44002001,
            ["abdm"] = 44002002,
            ["asla"] = 44002004,
            ["mcam"] = 44002011,
            ["amsm"] = 44002017,
            ["amna"] = 44002021,
            ["kmlt"] = 44002022,
            ["pnnb"] = 44002036,
            ["ssia"] = 44002039,
            ["asnm"] = 44002042,
            ["nnsd"] = 44002047,
            ["dsnp"] = 44003006,
            ["auks"] = 44003020,
            ["tnla"] = 44004010,
            ["askm"] = 44004012,
            ["gmuh"] = 44004017,
            ["astn"] = 44004018,
            ["shmd"] = 44004020,
            ["ansm"] = 44004031,
            ["nynp"] = 44005004,
            ["dnsp"] = 44005029,
            ["spnd"] = 44005031,
            ["mnkm"] = 44006003,
            ["alas"] = 44006007,
            ["asis"] = 44007010,
            ["dtad"] = 44007042,
            ["nyid"] = 44007045,
            ["dspp"] = 44007055,
            ["wkbn"] = 44008021,
            ["dnkf"] = 44008026,
            ["abnf"] = 44008035,
            ["dssk"] = 44009015,
            ["sgnn"] = 44009031,
            ["assp"] = 44010004,
            ["smbs"] = 44010038,
            ["sapn"] = 44010043,
            ["kins"] = 44011017,
            ["dmis"] = 44011020,
            ["sslm"] = 44011024,
            ["ansp"] = 44012011,
            ["dsen"] = 44013008,
            ["sbnt"] = 44013023,
            ["nmna"] = 44013043,
            ["anmg"] = 44014015,
            ["agma"] = 44014017,
            ["npam"] = 44014022,
            ["abdn"] = 44014026,
            ["ksts"] = 44015020,
            ["aibn"] = 44016014,
            ["sskp"] = 44016017,
            ["dnmn"] = 44016025,
            ["aksm"] = 44016031,
            ["nbap"] = 44017003,
            ["nlnm"] = 44017011,
            ["adng"] = 44017024,
            ["nhrn"] = 44017025,
            ["ukha"] = 44017027,
            ["sskt"] = 44017028,
            ["sasa"] = 44018010,
            ["atit"] = 44018025,
            ["dism"] = 44019013,
            ["ahlm"] = 44019027,
            ["dhkm"] = 44020024,
            ["anik"] = 44020032,
            ["nhas"] = 44020035,
            ["nmps"] = 44024015,
            ["dnma"] = 44024024,
            ["dspi"] = 44026007,
            ["dmka"] = 44026016,
            ["uima"] = 44026018,
            ["knau"] = 44026020,
            ["kpna"] = 44026023,
            ["mhns"] = 44028028,
            ["nina"] = 45001004,
            ["slnn"] = 45001007,
            ["shki"] = 45001016,
            ["sdak"] = 45001017,
            ["samb"] = 45001020,
            ["ohma"] = 45002004,
            ["dasi"] = 45002005,
            ["hnmo"] = 45003004,
            ["hnms"] = 45003006,
            ["abhg"] = 45003008,
            ["akla"] = 45003013,
            ["akba"] = 45003014,
            ["akmp"] = 45003015,
            ["kaka"] = 45003016,
            ["aadn"] = 45003017,
            ["wpsd"] = 45003018,
            ["nann"] = 45003019,
            ["dnbs"] = 45003021,
            ["smba"] = 45003022,
            ["sala"] = 45003023,
            ["pinw"] = 45003024,
            ["nsin"] = 45003025,
            ["spas"] = 45003026,
            ["knmn"] = 45003028,
            ["dskn"] = 45004005,
            ["gnns"] = 45004006,
            ["ddsp"] = 45004016,
            ["gnng"] = 45004017,
            ["spdn"] = 45005002,
            ["ahlg"] = 45005003,
            ["dind"] = 45005008,
            ["kkpn"] = 45005012,
            ["dgda"] = 45005015,
            ["sksp"] = 45005017,
            ["ukpa"] = 45005021,
            ["hnmt"] = 45006002,
            ["tnnn"] = 45006004,
            ["dktn"] = 45006008,
            ["gdnk"] = 45006011,
            ["ahdn"] = 45006013,
            ["saka"] = 45006014,
            ["naas"] = 45006019,
            ["sakn"] = 45006023,
            ["dntn"] = 45007006,
            ["knak"] = 45007012,
            ["knnk"] = 45007021,
            ["nnwn"] = 45008001,
            ["sama"] = 45008005,
            ["dkws"] = 45008009,
            ["aksc"] = 45008010,
            ["nkae"] = 45008011,
            ["aera"] = 45008016,
            ["snkn"] = 45008018,
            ["agdn"] = 45008026,
            ["annn"] = 45008028,
            ["aaas"] = 45008031,
            ["snhi"] = 45008032,
            ["samn"] = 45008033,
            ["sahs"] = 45008034,
            ["hksl"] = 45008037,
            ["sanl"] = 45008038,
            ["kakk"] = 45008039,
            ["knhs"] = 45009016,
            ["mkan"] = 45010001,
            ["ssca"] = 45010004,
            ["dsnk"] = 45010006,
            ["skim"] = 45010009,
            ["satn"] = 45010010,
            ["knap"] = 45010017,
            ["nkis"] = 45011006,
            ["mskn"] = 45011020,
            ["oknm"] = 45011033,
            ["knmk"] = 45012001,
            ["ahkm"] = 45012002,
            ["ssks"] = 45012003,
            ["agdt"] = 45012005,
            ["spsm"] = 45012010,
            ["hmts"] = 45012011,
            ["mspm"] = 45012012,
            ["mksn"] = 45012015,
            ["kmas"] = 45012018,
            ["hkmm"] = 45012019,
            ["hkps"] = 45012021,
            ["abka"] = 45013001,
            ["samp"] = 45013003,
            ["sihk"] = 45013009,
            ["aiyn"] = 45013011,
            ["mtmg"] = 45013013,
            ["kbin"] = 45013014,
            ["mnsi"] = 45014005,
            ["sknt"] = 45014008,
            ["skds"] = 45014015,
            ["mahk"] = 45014021,
            ["lnnd"] = 45015005,
            ["uknm"] = 45015006,
            ["sgmk"] = 45015007,
            ["pnkn"] = 45015013,
            ["uamm"] = 45015016,
            ["sind"] = 46001002,
            ["adat"] = 46001009,
            ["nbsc"] = 46001013,
            ["nasd"] = 46001014,
            ["abkr"] = 46001016,
            ["shas"] = 46001017,
            ["sasn"] = 46001018,
            ["snik"] = 46001019,
            ["snam"] = 46001020,
            ["sysk"] = 46001021,
            ["amjn"] = 46001022,
            ["daai"] = 46001023,
            ["kpnd"] = 46001027,
            ["uwln"] = 46001029,
            ["dskk"] = 46001030,
            ["aaap"] = 46002004,
            ["uaip"] = 46002005,
            ["dgnn"] = 46002009,
            ["namb"] = 46002013,
            ["nata"] = 46002014,
            ["ssan"] = 46002016,
            ["aamk"] = 46003001,
            ["apwa"] = 46003007,
            ["ssah"] = 46003011,
            ["hbnn"] = 46003016,
            ["kgns"] = 46003017,
            ["shms"] = 46003018,
            ["knhm"] = 46004005,
            ["ssag"] = 46004007,
            ["uaga"] = 46005005,
            ["agam"] = 46006011,
            ["ohbn"] = 46006019,
            ["skbs"] = 46006020,
            ["aabm"] = 46007013,
            ["gmkh"] = 46007015,
            ["sats"] = 46007022,
            ["shkb"] = 46007023,
            ["abma"] = 46007039,
            ["ntsm"] = 46008001,
            ["oabl"] = 46009006,
            ["alan"] = 46010004,
            ["nhdk"] = 46010010,
            ["hdsi"] = 46010013,
            ["kkkn"] = 46010031,
            ["mttk"] = 46011001,
            ["dikn"] = 46011003,
            ["awbk"] = 46011022,
            ["stks"] = 46011023,
            ["kiks"] = 46012003,
            ["nmii"] = 46012004,
            ["dsbi"] = 46012007,
            ["ssie"] = 46012013,
            ["uhmn"] = 46012025,
            ["dmnn"] = 46012031,
            ["kamn"] = 46013001,
            ["akma"] = 46013002,
            ["akik"] = 46013003,
            ["hnmh"] = 46013005,
            ["hnsk"] = 46013006,
            ["labl"] = 46013007,
            ["kann"] = 46014013,
            ["sipk"] = 46014016,
            ["dkwt"] = 46014028,
            ["aado"] = 46014029,
            ["kinn"] = 46014037,
            ["dspn"] = 46015010,
            ["sysp"] = 46015021,
            ["skpk"] = 46015022,
            ["hkpa"] = 46015033,
            ["gkna"] = 46015034,
            ["inmk"] = 46015044,
            ["akpt"] = 46015049,
            ["nsks"] = 46015051,
            ["siss"] = 46015052,
            ["dssd"] = 46015057,
            ["tuan"] = 46016002,
            ["nkds"] = 46016010,
            ["mkmk"] = 46016013,
            ["gnsp"] = 46016014,
            ["nsus"] = 47001004,
            ["dmkm"] = 47001006,
            ["okns"] = 47001009,
            ["npad"] = 47001018,
            ["smgm"] = 47001020,
            ["daip"] = 47002010,
            ["ssmi"] = 47002015,
            ["shkg"] = 47002017,
            ["aagp"] = 47003004,
            ["hska"] = 47003005,
            ["dtln"] = 47003018,
            ["nbnd"] = 47004004,
            ["shni"] = 47004005,
            ["ntna"] = 47004007,
            ["sman"] = 47004008,
            ["pgmh"] = 47004009,
            ["lsmt"] = 47004010,
            ["skna"] = 47004011,
            ["nymg"] = 47004013,
            ["saln"] = 47004015,
            ["skan"] = 47004018,
            ["snnn"] = 47005001,
            ["stns"] = 47005002,
            ["nkmn"] = 47005003,
            ["stnk"] = 47005004,
            ["nags"] = 47005005,
            ["knkl"] = 47005006,
            ["snks"] = 47005007,
            ["nmal"] = 47005008,
            ["stla"] = 47005010,
            ["hnim"] = 47005012,
            ["kkas"] = 47005017,
            ["smbn"] = 47005019,
            ["aykn"] = 47006001,
            ["ssns"] = 47006002,
            ["tsng"] = 47006010,
            ["hkns"] = 47006012,
            ["gmad"] = 47007006,
            ["nnah"] = 47007009,
            ["sakm"] = 47007010,
            ["nnir"] = 47007011,
            ["aiah"] = 47008005,
            ["dykn"] = 47008007,
            ["hann"] = 47008008,
            ["dntd"] = 47008011,
            ["kmmt"] = 47008023,
            ["dska"] = 47009006,
            ["mabi"] = 47009007,
            ["amgn"] = 47009008,
            ["ssrn"] = 47009014,
            ["ssdd"] = 47009015,
            ["sams"] = 47010004,
            ["nsgn"] = 47010005,
            ["nhni"] = 47010015,
            ["uiae"] = 47010016,
            ["dana"] = 47010017,
            ["sant"] = 47011002,
            ["nanb"] = 47011003,
            ["skyp"] = 47011004,
            ["knan"] = 47012010,
            ["tnam"] = 47012012,
            ["aagn"] = 47012015,
            ["snai"] = 47013005,
            ["skwa"] = 47013008,
            ["ddsk"] = 47013010,
            ["abnp"] = 47013014,
            ["spna"] = 48001001,
            ["snab"] = 48001003,
            ["annk"] = 48001006,
            ["nihi"] = 48001007,
            ["dkko"] = 48001008,
            ["sabn"] = 48001010,
            ["sais"] = 48001011,
            ["shkt"] = 48001012,
            ["kakl"] = 48001023,
            ["ayad"] = 48002004,
            ["smya"] = 48002005,
            ["damw"] = 48002006,
            ["sbna"] = 48002012,
            ["aaim"] = 48002013,
            ["nnam"] = 48002014,
            ["tmjs"] = 48002015,
            ["nksa"] = 48002017,
            ["kanm"] = 48002018,
            ["sasp"] = 48002019,
            ["ansk"] = 48002020,
            ["hknn"] = 48002021,
            ["ilai"] = 48003002,
            ["gnna"] = 48003006,
            ["kams"] = 48003009,
            ["ssnk"] = 48003013,
            ["usmg"] = 48003014,
            ["aknb"] = 48003021,
            ["dknk"] = 48003022,
            ["apak"] = 48003024,
            ["dndn"] = 48003025,
            ["wmjo"] = 48003028,
            ["akkk"] = 48003029,
            ["dnda"] = 48004004,
            ["aphk"] = 48004007,
            ["gmnp"] = 48004008,
            ["dnyn"] = 48004009,
            ["aysi"] = 48004014,
            ["knab"] = 48004016,
            ["dmai"] = 48004018,
            ["dkpy"] = 48004029,
            ["skap"] = 48005001,
            ["nasp"] = 48005002,
            ["opkm"] = 48005003,
            ["khkc"] = 48005004,
            ["stsp"] = 48005005,
            ["skcj"] = 48005006,
            ["apni"] = 48005008,
            ["amps"] = 48005010,
            ["namk"] = 48005011,
            ["sabk"] = 48005014,
            ["nkkr"] = 48005015,
            ["dkkp"] = 48005018,
            ["aham"] = 48005019,
            ["psdp"] = 48005020,
            ["dabn"] = 48005022,
            ["kpls"] = 48005023,
            ["aisk"] = 48005024,
            ["ktns"] = 48005025,
            ["mksp"] = 48006002,
            ["sabt"] = 48006005,
            ["dats"] = 48006006,
            ["sann"] = 48006008,
            ["ahtm"] = 48006009,
            ["knst"] = 48006010,
            ["dmns"] = 48006014,
            ["mkab"] = 48006018,
            ["ntin"] = 49001005,
            ["sink"] = 49001006,
            ["nskm"] = 49001007,
            ["spks"] = 49001010,
            ["trns"] = 49001011,
            ["ddar"] = 49001015,
            ["ahat"] = 49001016,
            ["uisi"] = 49001017,
            ["nkgk"] = 49001020,
            ["sknl"] = 49001021,
            ["aaln"] = 49001022,
            ["akbn"] = 49002001,
            ["ninn"] = 49002002,
            ["sgnm"] = 49002003,
            ["nadp"] = 49002004,
            ["atik"] = 49002006,
            ["usmp"] = 49002007,
            ["ssbk"] = 49002008,
            ["hspn"] = 49002009,
            ["stkg"] = 49002010,
            ["nknp"] = 49002012,
            ["dnkc"] = 49002013,
            ["aups"] = 49002016,
            ["nska"] = 49002021,
            ["nski"] = 49002022,
            ["namg"] = 49003006,
            ["ndga"] = 49003007,
            ["unsp"] = 49003010,
            ["aspw"] = 49003011,
            ["knik"] = 49003013,
            ["usii"] = 49003016,
            ["nsca"] = 49003017,
            ["amap"] = 49003019,
            ["nsmg"] = 49003020,
            ["nnsi"] = 49004001,
            ["nbka"] = 49004002,
            ["ipip"] = 49004005,
            ["idaa"] = 49004006,
            ["kspn"] = 49004015,
            ["ndsk"] = 49004016,
            ["akmn"] = 49004024,
            ["knpn"] = 49004025,
            ["kmah"] = 49004026,
            ["anah"] = 49004028,
            ["alnk"] = 49004031,
            ["amks"] = 49004032,
            ["knms"] = 49005001,
            ["stnl"] = 49005005,
            ["snpk"] = 49005008,
            ["mnkl"] = 49005015,
            ["nklm"] = 49005020,
            ["npks"] = 49005021,
            ["dkpa"] = 49005024,
            ["mlin"] = 49005025,
            ["gdnn"] = 49005028,
            ["ahia"] = 49005032,
            ["haps"] = 49006006,
            ["mnmm"] = 49006007,
            ["skmk"] = 49006010,
            ["mknb"] = 49006011,
            ["saap"] = 49006012,
            ["ddmk"] = 49006013,
            ["amrn"] = 49006017,
            ["nmks"] = 49006018,
            ["knsm"] = 49006023,
            ["abns"] = 49006024,
            ["sska"] = 50001008,
            ["aiik"] = 50001009,
            ["uika"] = 50001010,
            ["nmnb"] = 50001011,
            ["nikn"] = 50001012,
            ["aaks"] = 50001014,
            ["tnin"] = 50001015,
            ["aign"] = 50001016,
            ["dini"] = 50001017,
            ["angm"] = 50001018,
            ["asam"] = 50001020,
            ["ssga"] = 50001021,
            ["gmam"] = 50001024,
            ["aspi"] = 50001025,
            ["umai"] = 50001026,
            ["ailp"] = 50001027,
            ["ssii"] = 50001029,
            ["alna"] = 50002002,
            ["nhng"] = 50002003,
            ["htnb"] = 50002004,
            ["ksna"] = 50002009,
            ["uspn"] = 50002010,
            ["auin"] = 50002011,
            ["sdag"] = 50002013,
            ["gnal"] = 50002014,
            ["ukmw"] = 50002015,
            ["nnns"] = 50002016,
            ["okam"] = 50002017,
            ["asgd"] = 50002018,
            ["diks"] = 50002019,
            ["spnl"] = 50002021,
            ["nnna"] = 50002022,
            ["duas"] = 50002024,
            ["nikk"] = 50002025,
            ["knns"] = 50002027,
            ["tnns"] = 50002029,
            ["stap"] = 50003003,
            ["onal"] = 50003008,
            ["umks"] = 50003010,
            ["kats"] = 50003011,
            ["hsan"] = 50003012,
            ["mkhk"] = 50003013,
            ["nash"] = 50003014,
            ["knki"] = 50003015,
            ["lamt"] = 50003016,
            ["ikke"] = 50004002,
            ["oikr"] = 50004003,
            ["mkls"] = 50004004,
            ["mnai"] = 50004005,
            ["aakn"] = 50004007,
            ["kmka"] = 50004008,
            ["datn"] = 50004010,
            ["hssk"] = 50004011,
            ["mama"] = 50004012,
            ["lnmb"] = 50004013,
            ["akmt"] = 50004015,
            ["dman"] = 50004018,
            ["nksd"] = 51001003,
            ["nids"] = 51001006,
            ["nsrn"] = 51001008,
            ["ddkn"] = 51001009,
            ["nnsa"] = 51001012,
            ["nskn"] = 51001014,
            ["nsal"] = 51001015,
            ["asus"] = 51001017,
            ["asau"] = 51001018,
            ["aknn"] = 51001021,
            ["gmpn"] = 51001022,
            ["ktnk"] = 51001023,
            ["nnas"] = 51001024,
            ["nagm"] = 51001025,
            ["nsni"] = 51001028,
            ["nddn"] = 51001029,
            ["sikn"] = 51002001,
            ["umak"] = 51002002,
            ["isku"] = 51002004,
            ["sbsl"] = 51002005,
            ["kpnn"] = 51002006,
            ["nnan"] = 51002007,
            ["kmbs"] = 51002008,
            ["ankm"] = 51002013,
            ["npau"] = 51002014,
            ["snah"] = 51002016,
            ["nian"] = 51002017,
            ["sahm"] = 51002018,
            ["ahns"] = 51002019,
            ["kknn"] = 51002020,
            ["gnhd"] = 51002021,
            ["kknm"] = 51003001,
            ["inai"] = 51003002,
            ["pscn"] = 51003004,
            ["pnna"] = 51003005,
            ["nirn"] = 51003007,
            ["dnai"] = 51003008,
            ["dhmm"] = 51003011,
            ["mnkg"] = 51003012,
            ["mnss"] = 51003016,
            ["aaig"] = 51003017,
            ["mahn"] = 51003021,
            ["mamk"] = 51003022,
            ["aiga"] = 51003023,
            ["mpgn"] = 51004001,
            ["mkps"] = 51004002,
            ["ntid"] = 51004003,
            ["uiam"] = 51004004,
            ["mknm"] = 51004005,
            ["aipn"] = 51004006,
            ["nsas"] = 51004008,
            ["nkno"] = 51004009,
            ["asnk"] = 51004017,
            ["nkls"] = 52001002,
            ["naiw"] = 52001003,
            ["akns"] = 52001006,
            ["smsi"] = 52001008,
            ["ssra"] = 52001009,
            ["auha"] = 52001010,
            ["saai"] = 52002003,
            ["kkpk"] = 52002004,
            ["nnms"] = 52002006,
            ["kkan"] = 52002007,
            ["gdkp"] = 52002008,
            ["gnin"] = 52002011,
            ["adnd"] = 52002013,
            ["saaa"] = 52002019,
            ["skaa"] = 52002020,
            ["uash"] = 52003003,
            ["ddnn"] = 52003005,
            ["dnst"] = 52003006,
            ["ddnk"] = 52003007,
            ["snan"] = 52003008,
            ["gaai"] = 52003010,
            ["npnn"] = 52003011,
            ["upna"] = 52003013,
            ["nabi"] = 52004004,
            ["nshl"] = 52004006,
            ["sttn"] = 52004007,
            ["kanh"] = 52004008,
            ["dtsp"] = 52004009,
            ["skgn"] = 52004010,
            ["ukmn"] = 52004012,
            ["sktn"] = 52004014,
            ["sisn"] = 52004015,
            ["sapd"] = 52004016,
            ["kmtn"] = 52004017,
            ["kmks"] = 52004018,
            ["dtsm"] = 52005001,
            ["skra"] = 52005002,
            ["psnm"] = 52005003,
            ["nkmk"] = 52005004,
            ["skla"] = 52005005,
            ["hntm"] = 52005006,
            ["dpma"] = 52005008,
            ["sthi"] = 52005009,
            ["nnds"] = 52005010,
            ["ddkm"] = 52005011,
            ["dins"] = 52005012,
            ["ailn"] = 52005013,
            ["mkwp"] = 52005017,
            ["slnm"] = 52005018,
            ["hnpa"] = 52005019,
            ["hnha"] = 52005020,
            ["snal"] = 52005021,
            ["lnab"] = 52005022,
            ["apkl"] = 52005023,
            ["tysi"] = 52005024,
            ["mkin"] = 52005025,
            ["apks"] = 53001004,
            ["nith"] = 53001005,
            ["kibn"] = 53001006,
            ["nmsh"] = 53001008,
            ["ppsu"] = 53001010,
            ["ddda"] = 53001011,
            ["uapn"] = 53001012,
            ["uhkm"] = 53002002,
            ["hkpk"] = 53002003,
            ["nsan"] = 53002004,
            ["sahn"] = 53002007,
            ["akmm"] = 53002008,
            ["snak"] = 53002009,
            ["ambd"] = 53002010,
            ["addi"] = 53002011,
            ["umsl"] = 53002012,
            ["skit"] = 53002014,
            ["naap"] = 53002016,
            ["anai"] = 53002017,
            ["auka"] = 53003002,
            ["ntap"] = 53003003,
            ["ampk"] = 53003004,
            ["apnn"] = 53003005,
            ["nnmk"] = 53003008,
            ["hdsk"] = 53003009,
            ["snpm"] = 53003010,
            ["sana"] = 53003011,
            ["smgn"] = 53003012,
            ["agmh"] = 53003015,
            ["kpik"] = 54001003,
            ["nakn"] = 54001005,
            ["nnmm"] = 54001007,
            ["asen"] = 54001011,
            ["atsa"] = 54001014,
            ["tapa"] = 54001015,
            ["gmdd"] = 54001016,
            ["nshw"] = 54001017,
            ["abia"] = 54001018,
            ["nima"] = 54001019,
            ["nsmi"] = 54001020,
            ["unsl"] = 54002001,
            ["amha"] = 54002002,
            ["iman"] = 54002003,
            ["smid"] = 54002005,
            ["niak"] = 54002006,
            ["ndia"] = 54002007,
            ["ikna"] = 54002008,
            ["ksns"] = 54002010,
            ["abmn"] = 54002011,
            ["nhki"] = 54002012,
            ["niss"] = 54002015,
            ["tapk"] = 54003001,
            ["hmhp"] = 54003003,
            ["nmnk"] = 54003004,
            ["niah"] = 54003009,
            ["nkam"] = 54003015,
            ["awpd"] = 54003016,
            ["nhns"] = 54004001,
            ["spsp"] = 54004005,
            ["kims"] = 54004006,
            ["sdda"] = 54004010,
            ["hhns"] = 54004012,
            ["hapa"] = 54004013,
            ["mksm"] = 54004015,
            ["katn"] = 54005005,
            ["dkas"] = 54005008,
            ["hing"] = 54005009,
            ["iknm"] = 54005014,
            ["ammn"] = 54005017,
            ["pksp"] = 54005021,
            ["amkn"] = 54005024,
            ["aamm"] = 54006002,
            ["pnmt"] = 54006005,
            ["dakn"] = 54006006,
            ["swtd"] = 54006007,
            ["nktm"] = 54006008,
            ["saps"] = 54006010,
            ["diot"] = 54006011,
            ["nskk"] = 54006015,
            ["nsla"] = 54006016,
            ["amss"] = 54006017,
            ["nsmn"] = 54006018,
            ["nmsk"] = 54006019,
            ["otim"] = 54006020,
            ["nppn"] = 54006021,
            ["ktna"] = 55001002,
            ["nkkm"] = 55001004,
            ["nika"] = 55001005,
            ["ddai"] = 55001006,
            ["shtb"] = 55001007,
            ["hmni"] = 55001008,
            ["nssa"] = 55001009,
            ["nsbn"] = 55001011,
            ["ddnd"] = 55001012,
            ["imam"] = 55001013,
            ["inmn"] = 55001015,
            ["knsn"] = 55001017,
            ["pnsn"] = 55001018,
            ["inak"] = 55002001,
            ["aamb"] = 55002002,
            ["sknn"] = 55002004,
            ["akas"] = 55002005,
            ["amnn"] = 55002006,
            ["imas"] = 55002007,
            ["amsj"] = 55002008,
            ["nspk"] = 55002009,
            ["kata"] = 55002010,
            ["taps"] = 55002011,
            ["ktma"] = 55002012,
            ["kthm"] = 55002013,
            ["pmhn"] = 55002015,
            ["dima"] = 55002016,
            ["nmtt"] = 55002018,
            ["dsim"] = 55002020,
            ["kasn"] = 55002021,
            ["dlma"] = 55002022,
            ["ntma"] = 55002023,
            ["nsmk"] = 55002025,
            ["asms"] = 55002026,
            ["samt"] = 55003002,
            ["wkpm"] = 55003003,
            ["mlmm"] = 55003004,
            ["nlna"] = 55003007,
            ["akps"] = 55003008,
            ["nsma"] = 55003010,
            ["mpmp"] = 55003011,
            ["oaln"] = 55003012,
            ["damt"] = 55003013,
            ["amsp"] = 55003015,
            ["uatn"] = 55003017,
            ["sain"] = 55004006,
            ["nanm"] = 55004007,
            ["mkpn"] = 55004009,
            ["slla"] = 55004011,
            ["mkrn"] = 55004015,
            ["saup"] = 55004016,
            ["dapa"] = 55004017,
            ["ainp"] = 55004018,
            ["mkpb"] = 55004021,
            ["spsb"] = 56001002,
            ["ddik"] = 56001005,
            ["sdna"] = 56001007,
            ["kmms"] = 56001008,
            ["nnst"] = 56001009,
            ["smmm"] = 56001010,
            ["nakm"] = 56001011,
            ["snis"] = 56001012,
            ["apia"] = 56001013,
            ["nhms"] = 56001014,
            ["smal"] = 56001015,
            ["nmkn"] = 56002001,
            ["naml"] = 56002002,
            ["ngda"] = 56002003,
            ["ukma"] = 56002004,
            ["mmms"] = 56002005,
            ["imrn"] = 56002006,
            ["pnmn"] = 56002008,
            ["imsm"] = 56002009,
            ["hmkm"] = 56002010,
            ["nhym"] = 56002013,
            ["imsk"] = 56003001,
            ["nhmn"] = 56003002,
            ["strn"] = 56003003,
            ["nnmn"] = 56003004,
            ["nhds"] = 56003005,
            ["nkin"] = 56003006,
            ["usps"] = 56003007,
            ["atmm"] = 56003010,
            ["psks"] = 56003012,
            ["smms"] = 56003013,
            ["apdn"] = 56003014,
            ["spnb"] = 57001001,
            ["nals"] = 57001004,
            ["spkn"] = 57001005,
            ["kbkc"] = 57001008,
            ["gmas"] = 57001009,
            ["iksi"] = 57001010,
            ["nnup"] = 57001011,
            ["nsap"] = 57001012,
            ["niks"] = 45016017,
            ["dkwk"] = 57001014,
            ["smsg"] = 57001015,
            ["nhna"] = 57001016,
            ["kana"] = 57001017,
            ["nksn"] = 57001018,
            ["aspn"] = 57001019,
            ["okmn"] = 57001020,
            ["kssp"] = 57001021,
            ["dbal"] = 57001022,
            ["abna"] = 57001025,
            ["adnn"] = 58001001,
            ["pssn"] = 58001003,
            ["hbsl"] = 58001014,
            ["knnp"] = 58002001,
            ["pmtk"] = 58002003,
            ["npsn"] = 58002004,
            ["smsk"] = 58002010,
            ["amsl"] = 58002015,
            ["pnss"] = 58002018,
            ["dscg"] = 58003006,
            ["ssid"] = 58004004,
            ["mntn"] = 58004011,
            ["awan"] = 58004013,
            ["stwi"] = 58004015,
            ["mntm"] = 58004016,
            ["nssm"] = 58005007,
            ["kttn"] = 58006001,
            ["nmmn"] = 58006009,
            ["sada"] = 58006010,
            ["nhkm"] = 58006012,
            ["usdb"] = 58006018,
            ["nwaw"] = 58007003,
            ["ddns"] = 58007025,
            ["ahma"] = 58008011,
            ["npnc"] = 58009011,
            ["gpka"] = 58009014,
            ["adds"] = 58009015,
            ["skyt"] = 58010010,
            ["iatg"] = 58010016,
            ["tmnm"] = 58010022,
            ["naim"] = 58010023,
            ["atmu"] = 58010024,
            ["gkhn"] = 58010029,
            ["knba"] = 58010031,
            ["hnni"] = 58010035,
            ["naal"] = 58010038,
            ["nthd"] = 58010039,
            ["napa"] = 58011001,
            ["sspn"] = 58011002,
            ["span"] = 58011003,
            ["spsa"] = 58011004,
            ["spse"] = 58011005,
            ["akwp"] = 58011006,
            ["spsn"] = 58011007,
            ["sina"] = 58011010,
            ["aspa"] = 58011013,
            ["spns"] = 58011017,
            ["smby"] = 58011018,
            ["spin"] = 58011028,
            ["sphn"] = 58011031,
            ["nnbn"] = 58011034,
            ["tnmb"] = 58011035,
            ["sppp"] = 58011037,
            ["nsmy"] = 58011038,
            ["nind"] = 58011040,
            ["kynt"] = 58012001,
            ["nmns"] = 58012002,
            ["sdny"] = 58012003,
            ["spnp"] = 58012006,
            ["sktp"] = 58012010,
            ["npnb"] = 58012015,
            ["pnkh"] = 58012025,
            ["kpni"] = 58012028,
            ["mnap"] = 58013001,
            ["hnla"] = 58013002,
            ["mmns"] = 58013004,
            ["sjas"] = 58013008,
            ["hnkp"] = 58013009,
            ["dapn"] = 58013016,
            ["inks"] = 58013018,
            ["nadn"] = 58013020,
            ["ssna"] = 59001001,
            ["mkka"] = 59001002,
            ["nknn"] = 59001005,
            ["nhsm"] = 59001006,
            ["shin"] = 59001007,
            ["atmd"] = 59001008,
            ["aamd"] = 59001010,
            ["hsns"] = 59001013,
            ["kabt"] = 59001014,
            ["kmak"] = 59001015,
            ["hkpm"] = 59001016,
            ["abmk"] = 59001017,
            ["sksk"] = 59001018,
            ["nnim"] = 59001019,
            ["sagn"] = 59001020,
            ["kina"] = 59001021,
            ["dmtk"] = 59001022,
            ["skas"] = 59001023,
            ["kasa"] = 59001026,
            ["adnr"] = 59001027,
            ["mkky"] = 59002001,
            ["skmp"] = 59002002,
            ["dnmm"] = 59002005,
            ["gmki"] = 59002008,
            ["dkkn"] = 59002009,
            ["gais"] = 59002012,
            ["kaik"] = 59002015,
            ["aais"] = 59002016,
            ["gdna"] = 59002017,
            ["osni"] = 59002018,
            ["iasn"] = 59002019,
            ["dimb"] = 59002020,
            ["skpa"] = 59002026,
            ["hmga"] = 59003001,
            ["ssmm"] = 59003002,
            ["kani"] = 59003003,
            ["namd"] = 59003004,
            ["aadi"] = 59003006,
            ["sabu"] = 59003007,
            ["sbdl"] = 59003010,
            ["abbs"] = 59003011,
            ["hiak"] = 59003015,
            ["sksm"] = 59003016,
            ["nakb"] = 59003017,
            ["aabn"] = 59003018,
            ["knah"] = 59004003,
            ["kmmh"] = 59004004,
            ["oibn"] = 59004005,
            ["pnks"] = 59004007,
            ["mksd"] = 59004008,
            ["iatn"] = 59004012,
            ["sadn"] = 59004015,
            ["dnan"] = 59004016,
            ["knnt"] = 59005005,
            ["ihip"] = 59005006,
            ["mdnk"] = 59005008,
            ["knhn"] = 59005010,
            ["ntnm"] = 59005011,
            ["nhsl"] = 59005012,
            ["nbas"] = 59005013,
            ["msba"] = 59005014,
            ["mnks"] = 59005016,
            ["seai"] = 59005017,
            ["aann"] = 59005020,
            ["pnad"] = 60001003,
            ["simd"] = 60001004,
            ["niai"] = 60001006,
            ["uaps"] = 60001007,
            ["nhnn"] = 60001008,
            ["nita"] = 60001009,
            ["tspn"] = 60001010,
            ["nisk"] = 60001012,
            ["kiba"] = 60001013,
            ["ngnm"] = 60001014,
            ["nyba"] = 60001015,
            ["snkm"] = 60001016,
            ["akit"] = 60001017,
            ["nink"] = 60001018,
            ["knmd"] = 60001019,
            ["nnnn"] = 60001020,
            ["nspn"] = 60001021,
            ["dasn"] = 60001025,
            ["gnms"] = 60002002,
            ["nkms"] = 60002004,
            ["krnn"] = 60002005,
            ["sian"] = 60002006,
            ["dkil"] = 60002009,
            ["nnnp"] = 60002010,
            ["mmik"] = 60002011,
            ["kpsb"] = 60002013,
            ["ngnk"] = 60002016,
            ["inal"] = 60002017,
            ["makm"] = 60002018,
            ["sikk"] = 60002019,
            ["ssgb"] = 60002021,
            ["nshn"] = 60002022,
            ["nsra"] = 60002024,
            ["kkla"] = 60003008,
            ["samu"] = 60003010,
            ["atss"] = 60003011,
            ["asas"] = 60003013,
            ["dkmk"] = 60003014,
            ["kiab"] = 60003015,
            ["ntam"] = 60003016,
            ["slmk"] = 60003017,
            ["sscm"] = 60003018,
            ["nast"] = 60003021,
            ["nnkn"] = 60003022,
            ["uhnk"] = 60004002,
            ["sddi"] = 60004006,
            ["nawn"] = 60004007,
            ["nusl"] = 60004008,
            ["nask"] = 60004010,
            ["nkas"] = 60004011,
            ["mmhk"] = 60004012,
            ["kkmd"] = 60004014,
            ["pnak"] = 60005002,
            ["niis"] = 60005007,
            ["kmmk"] = 60005008,
            ["nsln"] = 60005009,
            ["mknh"] = 60005014,
            ["ndda"] = 61001004,
            ["oadd"] = 61001005,
            ["skni"] = 61001008,
            ["sywn"] = 61001009,
            ["kmkl"] = 61001010,
            ["kmha"] = 61001012,
            ["aikm"] = 61001013,
            ["opkn"] = 61001015,
            ["skah"] = 61001016,
            ["ssts"] = 61001017,
            ["aatn"] = 61001018,
            ["akam"] = 61001019,
            ["nmmi"] = 61001020,
            ["shsk"] = 61001021,
            ["nmnd"] = 61002001,
            ["amms"] = 61002002,
            ["skad"] = 61002004,
            ["aads"] = 61002005,
            ["aiam"] = 61002007,
            ["dlnn"] = 61002010,
            ["nmmm"] = 61002014,
            ["npsd"] = 61002015,
            ["npnk"] = 61002019,
            ["skpn"] = 61002020,
            ["smps"] = 61002021,
            ["umna"] = 61003002,
            ["ssnn"] = 61003005,
            ["nspd"] = 61003006,
            ["nasn"] = 61003007,
            ["dhnk"] = 61003008,
            ["hmap"] = 61003009,
            ["ddaa"] = 61003010,
            ["naha"] = 61003012,
            ["knmm"] = 61003014,
            ["gdns"] = 61003016,
            ["dmks"] = 61003018,
            ["nktn"] = 62001007,
            ["ksnt"] = 62001008,
            ["mkma"] = 62002001,
            ["asap"] = 62002002,
            ["dast"] = 62002005,
            ["ansn"] = 62002006,
            ["mmwa"] = 62002007,
            ["mibu"] = 62002008,
            ["ausk"] = 62002010,
            ["nans"] = 62002011,
            ["kskm"] = 62002012,
            ["kasm"] = 62002014,
            ["hnia"] = 62002015,
            ["aasa"] = 62002017,
            ["mmai"] = 62002018,
            ["hkks"] = 62002021,
            ["sask"] = 62002022,
            ["tsia"] = 62002024,
            ["anmk"] = 62002028,
            ["asmn"] = 62003003,
            ["agnk"] = 62003008,
            ["asin"] = 62003009,
            ["hgnc"] = 62003012,
            ["nntn"] = 62003014,
            ["dnna"] = 62003016,
            ["dasm"] = 62003017,
            ["dmnt"] = 62003019,
            ["skht"] = 62003020,
            ["mmkt"] = 62003021,
            ["aaah"] = 62003022,
            ["aiak"] = 62003023,
            ["aabe"] = 62004003,
            ["ksdm"] = 62004004,
            ["sssk"] = 62004005,
            ["tnsd"] = 62004006,
            ["mmmt"] = 62004007,
            ["ahua"] = 62004008,
            ["dnap"] = 62004009,
            ["naph"] = 62004010,
            ["dnnn"] = 62004013,
            ["dnsa"] = 62004017,
            ["wtsp"] = 62004018,
            ["tnss"] = 62004019,
            ["dann"] = 62005002,
            ["siap"] = 62005003,
            ["sasi"] = 62005004,
            ["asad"] = 62005005,
            ["iyns"] = 62005006,
            ["smtn"] = 62005008,
            ["aiap"] = 62005011,
            ["akna"] = 62005012,
            ["aian"] = 62005014,
            ["lnka"] = 62005017,
            ["amsh"] = 63001001,
            ["askn"] = 63001002,
            ["alnn"] = 63001004,
            ["smmn"] = 63001007,
            ["ksid"] = 63001010,
            ["sabs"] = 63001011,
            ["aman"] = 63001013,
            ["amkg"] = 64001001,
            ["main"] = 64001002,
            ["wndk"] = 64001004,
            ["mgma"] = 64001005,
            ["sdsp"] = 64001007,
            ["nnnt"] = 64001008,
            ["asni"] = 64001009,
            ["kkpa"] = 64001010,
            ["mhmt"] = 64001011,
            ["sdap"] = 64001012,
            ["mmba"] = 64001013,
            ["dikm"] = 64001014,
            ["sjna"] = 65001001,
            ["smit"] = 65001004,
            ["nkni"] = 65001005,
            ["aama"] = 65001006,
            ["gdas"] = 65001007,
            ["daam"] = 65001009,
            ["dami"] = 65001010,
            ["sans"] = 65001011,
            ["amip"] = 65001012,
            ["mmna"] = 65001013,
            ["aami"] = 65001014,
            ["uiap"] = 65001015,
            ["amim"] = 65001016,
            ["nkmm"] = 65001017,
            ["kpss"] = 65001018,
            ["amia"] = 65001019,
            ["aain"] = 65001022,
            ["aaii"] = 65001023,
            ["ndsm"] = 65001024,
            ["sidn"] = 65001025,
            ["aanj"] = 66001001,
            ["nsss"] = 66001002,
            ["sjsp"] = 66001004,
            ["amkj"] = 66001005,
            ["agtk"] = 66001006,
            ["aaaa"] = 66001008,
            ["aalu"] = 66001012,
            ["asgn"] = 66001013,
            ["aaku"] = 66001014,
            ["aakm"] = 66001015,
            ["askk"] = 66001016,
            ["ansa"] = 66001017,
            ["imna"] = 66001019,
            ["ahnp"] = 66001020,
            ["kamk"] = 66002005,
            ["nini"] = 66002006,
            ["ampa"] = 66002007,
            ["hmka"] = 66002010,
            ["nkai"] = 66002019,
            ["nasi"] = 66002022,
            ["asan"] = 66003001,
            ["mkap"] = 66003002,
            ["amad"] = 66003005,
            ["stma"] = 66003010,
            ["adnm"] = 66003011,
            ["amag"] = 66003012,
            ["alki"] = 66003019,
            ["aaan"] = 66004008,
            ["mkop"] = 66004011,
            ["anks"] = 66005006,
            ["asig"] = 66005010,
            ["nnnm"] = 66005012,
            ["pnmb"] = 66007009,
            ["annt"] = 66007010,
            ["nnsn"] = 66007012,
            ["sacn"] = 66007017,
            ["aaun"] = 66008004,
            ["ahai"] = 66011015,
            ["aada"] = 66011016,
            ["nnpk"] = 66011017,
            ["atab"] = 66012006,
            ["askd"] = 66012011,
            ["kmko"] = 66012012,
            ["napn"] = 66014012,
            ["shmo"] = 66015004,
            ["ssme"] = 66016014,
            ["mami"] = 66017014,
            ["kdsi"] = 66018008,
            ["mkts"] = 66018020,
            ["anad"] = 66019004,
            ["aans"] = 66019010,
            ["anan"] = 66020004,
            ["maba"] = 66020006,
            ["apna"] = 66021004,
            ["ayns"] = 66021005,
            ["amam"] = 66021007,
            ["nsmd"] = 66021008,
            ["adai"] = 66021009,
            ["adna"] = 66021010,
            ["nmim"] = 66021012,
            ["aalp"] = 66021021,
            ["ahan"] = 66021022,
            ["aaba"] = 66021023,
            ["ahpd"] = 66021027,
            ["namp"] = 66022012,
            ["mann"] = 66022014,
            ["aaea"] = 66022017,
            ["assm"] = 66022020,
            ["kinl"] = 1002024,
            ["apki"] = 1003015,
            ["aski"] = 2015026,
            ["naia"] = 2020010,
            ["hkys"] = 2023024,
            ["hkgn"] = 3019015,
            ["hnba"] = 3019031,
            ["atib"] = 3019034,
            ["anoi"] = 5004001,
            ["hndn"] = 5004002,
            ["mnkb"] = 5004023,
            ["skia"] = 5007004,
            ["amli"] = 5007025,
            ["atal"] = 5030019,
            ["niia"] = 5030020,
            ["pmab"] = 5031012,
            ["mkla"] = 6001007,
            ["kipa"] = 6024020,
            ["gnnp"] = 8002012,
            ["atsp"] = 10022004,
            ["apab"] = 10022047,
            ["akso"] = 11008023,
            ["kmsn"] = 11008061,
            ["nkka"] = 14007019,
            ["anpy"] = 14016007,
            ["sanh"] = 15008022,
            ["asea"] = 16008006,
            ["ntkn"] = 18019025,
            ["asta"] = 18028028,
            ["amak"] = 19022026,
            ["pmas"] = 19025005,
            ["hmaa"] = 19028003,
            ["hnam"] = 19031017,
            ["oina"] = 19031023,
            ["hnkm"] = 19032009,
            ["mkas"] = 19032010,
            ["kmsp"] = 19032011,
            ["iams"] = 19037007,
            ["mopk"] = 19040005,
            ["hmps"] = 19040011,
            ["mamn"] = 19040016,
            ["nmsa"] = 19073028,
            ["niop"] = 19086015,
            ["smab"] = 19112004,
            ["atnk"] = 19118015,
            ["uunk"] = 20001002,
            ["hkkm"] = 20001022,
            ["ksmn"] = 20001031,
            ["naii"] = 20002002,
            ["kmim"] = 20002005,
            ["nnpa"] = 20003019,
            ["akhs"] = 20003021,
            ["hmma"] = 20004013,
            ["innm"] = 20009006,
            ["paia"] = 20010025,
            ["msnl"] = 20012022,
            ["uait"] = 20022019,
            ["sknk"] = 21007014,
            ["slni"] = 21009001,
            ["okat"] = 21011008,
            ["imap"] = 22008006,
            ["mkna"] = 23001018,
            ["apnk"] = 23009007,
            ["kapa"] = 23009017,
            ["ndaa"] = 23012002,
            ["opia"] = 23025001,
            ["addm"] = 23030018,
            ["nasa"] = 23049004,
            ["iauo"] = 23049013,
            ["lnma"] = 23055007,
            ["amnm"] = 23061010,
            ["snat"] = 24001015,
            ["gasn"] = 24006016,
            ["ambi"] = 24009005,
            ["gmpi"] = 24018023,
            ["aamn"] = 24032040,
            ["aabi"] = 24033009,
            ["hmbn"] = 24033024,
            ["ials"] = 25003057,
            ["mbaa"] = 26018023,
            ["khkk"] = 26018030,
            ["swak"] = 26018032,
            ["aaia"] = 26037014,
            ["snas"] = 27003014,
            ["nkht"] = 27003018,
            ["nmls"] = 27006012,
            ["ansl"] = 27006020,
            ["amnd"] = 28010008,
            ["hnap"] = 29002001,
            ["mknk"] = 30005014,
            ["ikam"] = 30005015,
            ["hkml"] = 33007008,
            ["smsa"] = 33007017,
            ["nnin"] = 34001015,
            ["annl"] = 35002018,
            ["opan"] = 35003002,
            ["apsg"] = 36003005,
            ["gmmk"] = 37002004,
            ["nmss"] = 38004006,
            ["iswk"] = 39003014,
            ["mnan"] = 40004017,
            ["shka"] = 40010020,
            ["naan"] = 40011019,
            ["npya"] = 40011025,
            ["gpna"] = 40012012,
            ["akak"] = 40018008,
            ["anda"] = 40020008,
            ["lnnm"] = 40023003,
            ["akml"] = 40024030,
            ["siaa"] = 40026028,
            ["nmds"] = 40026036,
            ["apls"] = 40026049,
            ["swab"] = 41004022,
            ["apsj"] = 41008027,
            ["atsj"] = 41010049,
            ["anpn"] = 41012001,
            ["alai"] = 41012042,
            ["ulan"] = 42001079,
            ["assl"] = 42009042,
            ["nody"] = 42010021,
            ["aaps"] = 42013014,
            ["asrn"] = 42014012,
            ["atns"] = 42016002,
            ["snia"] = 42017033,
            ["damn"] = 42018013,
            ["akts"] = 43001036,
            ["pans"] = 43005014,
            ["snsj"] = 43005019,
            ["nkhk"] = 43005047,
            ["innj"] = 43006005,
            ["hkma"] = 43007024,
            ["nisn"] = 43007039,
            ["skns"] = 43008024,
            ["ksia"] = 43008044,
            ["ssjh"] = 43009003,
            ["apnw"] = 43020026,
            ["osam"] = 44002018,
            ["pnnn"] = 44002031,
            ["ausa"] = 44009017,
            ["asai"] = 44010042,
            ["asop"] = 44013010,
            ["annb"] = 44013046,
            ["atnp"] = 44020029,
            ["nbnn"] = 45001032,
            ["smns"] = 45002007,
            ["dkak"] = 45002010,
            ["hnma"] = 45006012,
            ["anmb"] = 45006015,
            ["dnml"] = 45006022,
            ["gdnm"] = 45007004,
            ["amnb"] = 45007013,
            ["daas"] = 45010008,
            ["oaua"] = 45012008,
            ["asbn"] = 46003010,
            ["sbmm"] = 46008005,
            ["nsga"] = 46008006,
            ["wbkm"] = 46009005,
            ["iksk"] = 46014005,
            ["knas"] = 46014027,
            ["spnn"] = 46015002,
            ["siks"] = 46015003,
            ["hnin"] = 47001008,
            ["dada"] = 47001023,
            ["psps"] = 47009013,
            ["inns"] = 47012019,
            ["aklh"] = 48002010,
            ["ommn"] = 48003001,
            ["htmm"] = 48005026,
            ["utmk"] = 49001012,
            ["nmis"] = 49002020,
            ["ktni"] = 49003002,
            ["npni"] = 49004003,
            ["dabi"] = 49004007,
            ["uthn"] = 49004014,
            ["ksng"] = 49005014,
            ["npsb"] = 50001004,
            ["gnmn"] = 50001007,
            ["kbhn"] = 50002007,
            ["psmp"] = 51002015,
            ["hmpa"] = 54004014,
            ["nman"] = 55003005,
            ["nshm"] = 55003009,
            ["knna"] = 58002009,
            ["kmbn"] = 58003001,
            ["stna"] = 58004003,
            ["nhnp"] = 58010025,
            ["spss"] = 58011011,
            ["ninm"] = 58011019,
            ["akjn"] = 58012024,
            ["hbkn"] = 59002004,
            ["hbaa"] = 59002021,
            ["mbmk"] = 59003012,
            ["osmg"] = 60002014,
            ["snup"] = 60003005,
            ["kpns"] = 60004001,
            ["nhma"] = 60004015,
            ["nkks"] = 66002013,
            ["mkno"] = 66002016,
            ["abks"] = 66002021,
            ["nmki"] = 66003004,
        };


        private static readonly Dictionary<int, string> ReverseSCommands = SCommands.ToDictionary(x => x.Value, x => x.Key);

        public string AliasForThisVerse(int encodedBookValue)
        {
            if (ReverseSCommands.TryGetValue(encodedBookValue, out string alias))
            {
                string result = "S-Mode command: " + alias;
                return result;
            }
            else
            {
                return "S-Mode command: No alias found";
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

        public State getState()
        {
            return state;
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

            if (state == State.Query || state == State.Filter || state == State.Choice)
            {
                return Search(k);
            }
            //termination keys
            if (parseState == ParseState.TeleportingToMark)
            {
                command = new Command { Mode = State.S, Quantity = -1 * (int)k, Direction = Direct.Absolute };
                ShowMarks?.Invoke(false);
                validity = EntireCommandStatus.Valid;
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
                ShowMarks?.Invoke(true);
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
                
                if (parseState == ParseState.Start)
                {
                    //movement
                    if (k == 'j' || k == 'k')
                    {
                        return FeedMovement(k);
                    }

                    //mode switching in book state
                    else if (k == 'c')
                    {
                        state = State.C;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == 'v')
                    {
                        state = State.V;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == ';')
                    {
                        state = State.S;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == '/')
                    {
                        previousStateBeforeSearch = state;
                        justANormalStateChange = true;
                        state = State.Query;
                        EnterQueryState();
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                }
                return NotValid();

            }

            //mode switching
            if (parseState == ParseState.Start)
            {
                switch(k)
                {
                    case 'b':
                        state = State.B;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'c':
                        state = State.C;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'v':
                        state = State.V;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case ';':
                        state = State.S;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case '/':
                        previousStateBeforeSearch = state;
                        state = State.Query;
                        justANormalStateChange = true;
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
                        case 'y':
                            return CopyAll();
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
                            return BackgroundChange(Direct.Up);
                        case '\u0003':
                            return BackgroundChange(Direct.Down);
                        case '0':
                            return InterfaceChange();
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

        private EntireCommandStatus BackgroundChange(Direct direction)
        {
            if (direction == Direct.Up)
            {
                command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 700 };
            }
            else
            {
                command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 800 };
            }
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus Search(char k)
        {
            //toggles on and off SEARCH mode
            if (k == '/')
            {
                HideTheEnterBox?.Invoke(false);
                justANormalStateChange = true;
                SearchUiVisibilityChanged?.Invoke(false); // Close search UI
                state = previousStateBeforeSearch;
                commandString = "";
                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //tab character (switching focused ui's)
            else if (k == '+')
            {
                justANormalStateChange = true;
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
            else if (k == '\u0003')
            {
                commandString = "";
                justANormalStateChange = true;
                state = state switch
                {
                    State.Filter => State.Query,
                    State.Choice => State.Filter,
                    State.Query => State.Choice,
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
                else if (k == '\u0006')
                {
                    if (searchQuery == "")
                    {
                        return NotValid();
                    }

                    // Remove trailing whitespace first.
                    searchQuery = searchQuery.TrimEnd();

                    // Remove characters up to the previous whitespace.
                    int lastSpace = searchQuery.LastIndexOf(' ');

                    searchQuery = lastSpace >= 0
                        ? searchQuery[..lastSpace]
                        : "";

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

            //choice mode
            else if (state == State.Choice)
            {
                //commandString = "*" + choiceCommandBuffer;

                if (normalChoosingMode)
                {
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

                        case '\u0001':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 40 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '\u0002':
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 40 };
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
                            commandString = "*" + choiceCommandBuffer;
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'c':
                            normalChoosingMode = false;
                            choiceCommandBuffer = "";
                            commandString = "*";
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
            if (k == '.' && parseState != ParseState.ReadingQuantifier)
            {
                quantifier = previousQuantifier;
                return CompleteMovement(Direct.Absolute);
            }

            if (parseState == ParseState.Start && FastQuantifiers.TryGetValue(k, out int num)) {
                quantifier = num;
                previousQuantifier = num;
                return CompleteMovement(Direct.Absolute);
            }

            //user (may have) typed an alias to a number
            if (Quantifiers.TryGetValue(k, out int digit)) 
            {
                quantifier = (quantifier) * 10 + digit;
                previousQuantifier = quantifier;
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //user typed an actual number
            if (char.IsDigit(k))
            {
                quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                previousQuantifier = quantifier;
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

        private EntireCommandStatus CopyAll()
        {
            command = new Command { Mode = state, Quantity = 900, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus InterfaceChange()
        {
            command = new Command { Mode = state, Quantity = 1000, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void AFilterWasChanged()
        {
            HideTheEnterBox?.Invoke(true);
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
            commandString = "*" + searchText;
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 1, SearchText = searchText };
            validity = EntireCommandStatus.Valid;
            return validity;
        }
    }
}
