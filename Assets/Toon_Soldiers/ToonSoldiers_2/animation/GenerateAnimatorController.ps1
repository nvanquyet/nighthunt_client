# ========================================================
# Unity Animator Controller Generator - ToonSoldiers_2
# Architecture:
#   Layer 0 - Base      : Full-body locomotion per weapon (8-dir blend tree, crouch, prone, guard, jump, ladder, roll, sprint)
#   Layer 1 - UpperBody : Combat actions per weapon + stance (shoot/reload/draw/grenade/interact/takeDamage)
#   Layer 2 - Death     : Full-body death per weapon (AnyState triggers, DeathIndex selects clip)
# Parameters:
#   WeaponType  int  0=Handgun 1=Infantry 2=Heavy 3=Knife 4=Machinegun 5=RocketLauncher
#   VelocityX   float  -1..1
#   VelocityY   float  -1..1
#   Speed       float  0..2 (0=idle,1=walk,2=run – used for Guard blend tree and sprint threshold)
#   IsCrouching bool
#   IsProne     bool
#   IsGuard     bool   (ADS / raised weapon)
#   IsSprinting bool
#   IsGrounded  bool
#   IsOnLadder  bool
#   Shoot       trigger
#   ShootBurst  trigger
#   ShootLoop   bool   (hold-to-fire for Heavy/Machinegun)
#   ShootBolt   bool   (toggle for Infantry bolt)
#   ShootShotgun bool  (toggle for Infantry shotgun)
#   Reload      trigger
#   Draw        trigger
#   ThrowGrenade trigger
#   Interact    trigger
#   InteractIndex int  0=A 1=B
#   TakeDamage  trigger
#   Attack      trigger  (Knife only)
#   AttackIndex int   0=A 1=B
#   Die         trigger
#   DeathIndex  int   0..4
# ========================================================

$outputPath = "w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"

# ---- clip helper ----
function Clip($g) { "{fileID: 7400000, guid: $g, type: 3}" }
$NC = "{fileID: 0}"

# ---- GUID map ----
$G = @{
# Handgun - Standing
HG_idle="2e680f73a503c59439a46e36fa7f532c"; HG_walk="5c6e0502d520aa54390ef1e20793c9cf"
HG_walk_back="a6a7423655a2de048b3910eb7a83c626"; HG_walk_left="e738d9ebbdb3b4c41a3e03056acac4cf"
HG_walk_right="ae6610ca92937e34d96b78426ca6e0e4"; HG_run="0612147567bd2594b8e577c8acf8caf9"
HG_run_back="69c183cbc4545454489c9b2082423f6c"; HG_run_left="df5e48257ebcd01438fe56a4e918bc1f"
HG_run_right="e92f30610b67c864fbe0a02a1c851e56"; HG_sprint="c66452fbe23e01c4ca8154a2d5608766"
HG_roll="4c00ec9d606710c41b6765c90e0c2cfe"
HG_jump1="a2bbfd63924cb54488fd015b913821b0"; HG_jump2="0142680b4ed5a96489ea5a3d75fd2eed"; HG_jump3="e923d5eed28ed3e41956f0abe958bd57"
HG_lad_up="2f7f9bac6be0a414b8f8e37a10fdd3ba"; HG_lad_idle="d3f9ded8d319fd34a85a3cfd27d1d593"; HG_lad_down="f9e294b3c811f97478b9b2a47596ef9a"
# Handgun - Crouch
HG_c_idle="0237a731ef257ae4aa09b06a37e3318c"; HG_c_walk="13e9832c61f31e9449dbdbb66fac9d53"
HG_c_walk_back="163adf1cf5d540e48a903668f11c1527"; HG_c_walk_left="2f7d2805108d96f4d903a920970d252a"; HG_c_walk_right="182e9d9d9d3c2ed4da618b0775e4aa79"
# Handgun - Guard
HG_g_idle="bf601ba5c09ddd34a86eb2731af868c1"; HG_g_walk="a56510c9caa07a14795dbca477204500"; HG_g_run="8781c525baf69ac4186502d2afecb593"
# Handgun - Prone
HG_p_goto="aab0abc42b5867747aee3f41bce55da0"; HG_p_idle="b2d13125d1be5494889b20ce6a6ca4bb"
HG_p_move="257d2175eff4c774e94bb9de7d7feb13"; HG_p_standup="d1199c4aa1731d5479b3699fa09ee2c3"; HG_p_pdeath="ca752262582e0134ab39790c4cca91b2"
# Handgun - Combat (stand)
HG_draw="a2e1100fab7269a43a702bdae9c5c511"; HG_shoot="354a7204e11f37e40bf5fb5f281167bd"
HG_shoot_burst="362255460a9049e48a1e7ebf0d696f36"; HG_reload="e77f4ee8d24674a4bb659b470d5939a4"
HG_grenade="5d963d18496c0b34e856fd40baabb89e"; HG_intA="4734bef5b1f04cc4192d64ef01051a87"; HG_intB="e5f6c13743887d74c86555fa052b020d"
HG_damage="4029b0e09f4b2e44380368978a5bc662"
# Handgun - Combat (crouch)
HG_c_draw="c71df1ffcd0b2494c8408b7fa9bf7946"; HG_c_shoot="0d5cf4b5f3ad0f84aa1f534c1141b894"
HG_c_shoot_burst="982a04306ed48c3499986dde42beac5e"; HG_c_reload="c2526a87c6bb4364783361724fd294d1"
HG_c_grenade="cc4a8be99dc8c8a4d9893978440bc4fc"; HG_c_damage="177f018239ade9f4aa2a3956ef45d0bb"
# Handgun - Combat (prone)
HG_p_draw="f17d9d76c3dfbff4ba44e6500ba1976f"; HG_p_shoot="617a5cf40ce82e34f97309538073a922"
HG_p_shoot_burst="0d061450b6396c643ac8634001e578cd"; HG_p_reload="fdb675dece10a3e4ba56828a8ea61012"
HG_p_grenade="760d8bbf5f0849f4c97ab833a5129a64"; HG_p_damage="1b985d15c9e44fe4ca7aa5fb9104c7cb"
# Handgun - Deaths
HG_dA="137155502bade354080c0c9ccdf60d9f"; HG_dB="56c88776f884dda4a948b1b65325eefd"
HG_dC="d7583335a6287e547afc1f3d264ce9cf"; HG_dD="d22c614baf27e7b43bf90f1abc111163"; HG_dE="4404c374a950ef546835eb97b753fc5f"

# Infantry - Standing
IN_idle="e7dad051bd467364291629d1c5486566"; IN_walk="08e0e8b51f66d16478cb29c5093679b9"
IN_walk_back="9e1b247aee575b94991c6e2fdd777f33"; IN_walk_left="babbde8691dbaf4409d4d0c9810017c7"; IN_walk_right="3e9d599d8992a7e4a9a8c0d5009b8ffa"
IN_run="6008119bb2e891142b7f0fe6f6283910"; IN_run_back="5b290596ca21e324aa3d3de37293f527"
IN_run_left="bb23a0bf57d23ab4c992210d7949b39f"; IN_run_right="08d47cd7dfe0bac4982f79367268cf18"
IN_sprint="b4a9d2c6d9ad19f49864830150529ea1"; IN_roll="b471079f1e19d3244a65101131b2784b"
IN_jump1="b05b132419ea08242baa61b5bddf05ec"; IN_jump2="396e70e9168a0cf4381e61151661b3cd"; IN_jump3="86b7741a43ceeea498054a6d664498b8"
IN_lad_up="1e6c7d0931e18b94b8c3f96017cb440a"; IN_lad_idle="e2b9f145d077b744fa60d7eb3d5e3c01"; IN_lad_down="63c93ff430450dd409498b46f620790d"
# Infantry - Crouch
IN_c_idle="56d548a0990d7a549a46a304d5e227aa"; IN_c_walk="d9b66aacfd3922344ab1635e8e7fe372"
IN_c_walk_back="f0b5d1045f0f5d34390d7d51c9eda346"; IN_c_walk_left="6abae466c5aca9a43990d8e4db32c66c"; IN_c_walk_right="34ce6be95a5cbc64a9c5c72165fdd933"
# Infantry - Guard
IN_g_idle="c05c3153c6deb1649a746854894e14e1"; IN_g_walk="f5838e6b1ba8d104f800ea1a2fd726c1"; IN_g_run="1c5e35e4bb2d828468923bf1d605ac60"
# Infantry - Prone
IN_p_goto="15dbac04bb57a8f45984870a56b9cc9d"; IN_p_idle="d55dda5c3fa9dd9449944c2f6ac9843d"
IN_p_move="5a05f67b1ba4a504cade4d8f76528ec9"; IN_p_standup="c422255fb670c0b47ae410395aff87f7"; IN_p_pdeath="45e3c7da6735c194893cc29af2bf2ac0"
# Infantry - Combat (stand)
IN_draw="a8e2d9630991c6e458e0a87cf39d29c1"; IN_shoot="4235e9150ffeb904e8529941222a20b7"
IN_shoot_burst="08874e94c96227746815534b4b44fe37"; IN_shoot_bolt="e43ab61933d857f4fb810c6d40cbc9f9"
IN_shoot_shotgun="d83cd7c874fb3cd4c8893275ed62a117"; IN_reload="74d22c24b0b562b4994a0b3fb7918e98"
IN_grenade="217c0835b1e8a3a49b17d10bd6283b32"; IN_intA="16ee7a8bfc284194095d0f8f07fae95a"; IN_intB="aa731413adc351e44a3fdc2d67ad8572"
IN_damage="e652d993098954549bd4593503d5e296"
# Infantry - Combat (crouch)
IN_c_draw="f7d5146bf379ddf40b6ee10c11cfe851"; IN_c_shoot="4981a8e3bb0cab7469c40ff30e919917"
IN_c_shoot_burst="3439080d05eb0624d985c38efa2147a4"; IN_c_shoot_bolt="76a1b74ace3c271438a75d46f4a09c36"
IN_c_shoot_shotgun="3b97b14a269badf448ee5119606c2c66"; IN_c_reload="950822c36a39ae547b51f4f92b8f287c"
IN_c_grenade="52ac426a45e7f9a4494dd07090069c39"; IN_c_damage="b29dae0cb9dd7a945bc547722965cd0d"
# Infantry - Combat (prone)
IN_p_draw="6b55d1b6df258d849ac3b13ad7199d89"; IN_p_shoot="5f73acd18dd82654b86804c1c5b69390"
IN_p_shoot_burst="281ad7eec0cbbb54c8e6124ba3357911"; IN_p_shoot_bolt="6cc3f8ab1c8e3e04cbdee146a7cc4db4"
IN_p_shoot_shotgun="a594fda61bd662e45af4d36806b2ce07"; IN_p_reload="d88014aa4008f7a429a0e0d666961dec"
IN_p_grenade="4d7e45c55c6036042b1119b2a2e5b8af"; IN_p_damage="0791381efd0681c4cbbeec69508c7bb2"
# Infantry - Deaths
IN_dA="e8ce44c146300d840a88117f1e211e11"; IN_dB="ce6250afac435fb4a90f03ac31030f73"
IN_dC="440762e66d757c243a52ba002631ead8"; IN_dD="a72cca69902f4494e84a3833b17ea2fa"; IN_dE="dbd86a83eed1db54fa287556091e1138"

# Heavy - Standing
HV_idle="ce8cd41dadb9f2f45932dde0cea99030"; HV_walk="883a074e6f60c0d4f938c55d4562a23f"
HV_walk_back="fb876b95f18aeed48b2a04e09f15f820"; HV_walk_left="7a9097e84cd284141b2517e2bfd69d90"; HV_walk_right="c4835b06ac7c6874aa0df6a762fd8852"
HV_run="d3a7b522641069a44be7a391faa17775"; HV_run_back="68465b08c5fd4a541a2569ee8317a3f6"
HV_run_left="3d06aa99da6483d4780d2ce51602f160"; HV_run_right="9c9102fe1386b1940ace22aad237a5e5"
HV_sprint="8266d475fe885c248a5e8d4d987faa22"; HV_roll="e642838ae4d1c2d4e86c9a7a51ae631f"
HV_jump1="fd9a856256c5c234aafd87637027101c"; HV_jump2="b679a8f02538c034297403cf430e27d6"; HV_jump3="2f2330ec3b15b4a4280e2b567df8ab60"
HV_lad_up="9d3e933cc72ed284fb890d9ed6c37ee8"; HV_lad_idle="edd2ec1ec24d8554a85c73b7b0257dc9"; HV_lad_down="c65c1330172495b45848d5578c86d1ef"
# Heavy - Crouch
HV_c_idle="148244b934e772e4f8a1dec7d0b5a40d"; HV_c_walk="ed0454e7390e0f54a9f1408697b925c3"
HV_c_walk_back="55c671bd3ce009546ad4d51b398ff479"; HV_c_walk_left="ea6fc694d23a0164b9bb8fc88f926d67"; HV_c_walk_right="282edebadee5f5648a3a3a5336e10f8f"
# Heavy - Guard
HV_g_idle="f2e5dcf9a0bf37149931058205fb64ee"; HV_g_walk="178fcac623b95aa40acc17f94fd644b1"; HV_g_run="a6f998b777e31c340a529e5d84b1467b"
# Heavy - Prone
HV_p_goto="56d00a711ebb1904f98081f5e40ac5f4"; HV_p_idle="04cdfdcc3a234904bbf2afdce78436c9"
HV_p_move="f396646ec2c8938438eaae9197d776a8"; HV_p_standup="aaddff8d8b27d724c889076ef599d382"; HV_p_pdeath="9504f04fca7ff9c4984613e79a7e3cd6"
# Heavy - Combat (stand)
HV_draw="9b4642734aec86a478befeaf066f3c30"; HV_shoot="e829126bc8b38b947a3c2996b2001db2"
HV_shoot_burst="bef763a9da5ad934bafb908fb258622d"; HV_shoot_loop="893f7b8488dd63c4db1fac30847df9c0"
HV_reload="de6f782cb998f3d41a9e756ef3aad689"; HV_grenade="fef5fb8c7320b7549be5c532476c34b4"
HV_intA="064962ee4a1e42649a06ec12b348994d"; HV_intB="80e6a0718f9d25f4ba0e2ac867e4251d"; HV_damage="d59c524607e85e7408f6852c04a8a211"
# Heavy - Combat (crouch)
HV_c_draw="e8541312adfc6cc48ab4d9a3cad0a985"; HV_c_shoot="ede7271215c7f4d44bea2c3819cfc441"
HV_c_shoot_burst="a8437b18f11c51c46abda54b3088b234"; HV_c_shoot_loop="0905efa0dc774014981e11c094970243"
HV_c_reload="ddf0e2f504705f545809956d7c1f3e12"; HV_c_grenade="4de2dc76c2d15cc45ac4550d523bb9a6"; HV_c_damage="ac3213a3f281b3044b7f6165990b6aa0"
# Heavy - Combat (prone)
HV_p_draw="d6269c0800c987f47852dd8a09050753"; HV_p_shoot="20434eeea3821f947bc4b656cb374446"
HV_p_shoot_burst="df078382c8241284580dab7ec67abf51"; HV_p_shoot_loop="5145607d26bba374b92a6f7d8e8fb4b2"
HV_p_reload="439c0ee348b0a90419c334d8cb265a02"; HV_p_grenade="52ea9e1083fbaf44282b6aab188d776f"; HV_p_damage="8eeb9d13dce6ba44a970ad062b1e319b"
# Heavy - Deaths
HV_dA="97036ca30f1972049a85fa2b0af71dab"; HV_dB="163a0cc489d32f24c97f8d0c11aaff8f"
HV_dC="42e015c6c96ea3343918d8d19334f73d"; HV_dD="6f428385bcf7ca04da1bf6cd03473bfa"; HV_dE="e0ba8a4ce854b8147a16b48603755bcf"

# Knife - Standing
KN_idle="de10cb89874e73c49b10494f519e107c"; KN_walk="92baf4e3dcb24dc4784883394a0df42b"
KN_walk_back="edf82b7aa596df44a962ba0095360b17"; KN_walk_left="808a91b48d57cbf41a5edded266c4364"; KN_walk_right="5102031d78dc5564491f2f3a51fe2c4b"
KN_run="d87f619229258c84bbf56552c6530cab"; KN_run_back="7270c359a9e0fde4fa309dd3bebf5c4b"
KN_run_left="9a59c9a1be1eec04aad68b3a56a650c7"; KN_run_right="8f55c32bfa9d2db4580ab57832390794"
KN_sprint="99e9395a39c2da340a7f972f949e383a"; KN_roll="130d54aeaa61d0f41b8bff297af8f4b1"
KN_jump1="e1c985ecefb06fa40a7e911682475223"; KN_jump2="024957608f411294d8c99a5abdbef8c4"; KN_jump3="543115b0d12c0c44d85582cb8b91b4fe"
KN_lad_up="bdb5df44cbd6ec24f9f8592f3e718a4e"; KN_lad_idle="6c651740bdd133f4a8d2942733e28da0"; KN_lad_down="60938c2d7d03ca84585d11fe3edf8dbb"
# Knife - Crouch
KN_c_idle="06c5057d270b8cd478b43046fc51f5ce"; KN_c_walk="4deae1de633453d4781243a06b6bcf39"
KN_c_walk_back="199cf54776c5bab44a876ed346291988"; KN_c_walk_left="3667a2eef03155946a03a41451768348"; KN_c_walk_right="3bd0476eaa0f2c04bab605bc10a78b80"
# Knife - Guard
KN_g_idle="69626d52ce3ce7c4a9c0a214345c0f01"; KN_g_walk="ee54f170fca0c1d45a849e34b494e76e"; KN_g_run="af99955a909752e49ad3d4d7a99e881f"
# Knife - Prone
KN_p_goto="8725c1b589fdec94cab02776b7a9b131"; KN_p_idle="2f734c64f95f1c14e9baec2f4a832fe9"
KN_p_move="134f8ffe80c17a54aa81024847c16995"; KN_p_standup="6411c433c999ebe489543bbe4a17dee4"; KN_p_pdeath="3051f09989045ba4ea602878448b478c"
# Knife - Combat (stand)
KN_draw="009466af285802541891e704a447f9bb"; KN_attackA="6b1b3f222fdcf014f8710050a779544e"
KN_attackB="14a6cfa2b968de049a6c79eb177c72d0"; KN_grenade="059b0a255beb0d04c842537e62fc805e"
KN_intA="945227cdd5e36604380367a85d6b621b"; KN_intB="5ec14f480c5f4934bb2fab62bed900e2"; KN_damage="e053dffcdff2b4f41af8bc661ef5aeee"
# Knife - Combat (crouch)
KN_c_draw="d860be37c402e624ab142bde1a1feb5a"; KN_c_attackA="52d5e57f1eb5ec14fa664a04cacc2108"
KN_c_attackB="25790d786874b0d4792193300a38ab87"; KN_c_grenade="23c548267446a664ea4a2e79165b2fc4"; KN_c_damage="62bdef63dbb179e49a87da47617e4277"
# Knife - Combat (prone)
KN_p_draw="bcc606be38c35e84eafff3651c0a2832"; KN_p_attack="e5e780270acfb3b4ba7a145d999b3ee0"
KN_p_grenade="00994246e9fc3f44d908f593795a5451"; KN_p_damage="8ae86bea5dcdf3e48a89cd9ffa0b5f43"
# Knife - Deaths
KN_dA="e476bbf2d4a24364e81efa579658ec2b"; KN_dB="9325b40f6d6591f4f8399b21c2ff39a1"
KN_dC="6d2500a62805f3f43b8e188aeb619ab5"; KN_dD="234019a2e5a14804cabc3f641768811d"; KN_dE="2b9cf50656fb0604eb0479483bbaaa97"

# Machinegun - Standing
MG_idle="09cf9cc91e070f34598d05d6f6e92c8d"; MG_walk="35f27f220d106674f88940ee0e5d8962"
MG_walk_back="443bd91bfc3b762468c5e9f4ce1260ef"; MG_walk_left="490db73c2cba5e441ae9a88ac0203206"; MG_walk_right="a1b718bd2a4f5f44081c737ad7e7015c"
MG_run="f6c60814507519a449316acda7f22e39"; MG_run_back="6252f9b6e33c57a4289d72af06a29647"
MG_run_left="d2bda8fe490dabc41b02d18ed2f6ac00"; MG_run_right="c519f6d80c208ff44baf2b1a00b29458"
MG_sprint="80efaeb0e3055144e928e2bc271539a2"; MG_roll="b31106ea8fa3f9d4cb7ed5147d630838"
MG_jump1="1f79f2e2c2b96d94f97b870d3e2eb21e"; MG_jump2="0aa0c76993c88504ba1f4a2507940fa6"; MG_jump3="5cc7cf9e174e53e4c89a901ad79772a7"
MG_lad_up="26d9d79e9ff470b44993732e7d22a4bc"; MG_lad_idle="8370244f4df1834448a3716f0ef86919"; MG_lad_down="6f42cfb2508926247932373b19d7277c"
# Machinegun - Crouch
MG_c_idle="557ca7f50cf89c14faffaa0fd5d5913b"; MG_c_walk="a81ae83587c682a43b408110700039ee"
MG_c_walk_back="7ce25ef60324be94bbbfc4a79d6234e8"; MG_c_walk_left="30d0ea6e414edca45a9f20eddb4c5fd1"; MG_c_walk_right="7d3de712c09f96648be17886aa088a1b"
# Machinegun - Guard
MG_g_idle="3400499e1b4b62d4ca3c2b4e85243add"; MG_g_walk="4cbae14935d224446b703f3282793b90"; MG_g_run="8b5552854ce35a846b41cc0e197b8791"
# Machinegun - Prone
MG_p_goto="78cbc4848b997be4eb5a3ebc0ba4b931"; MG_p_idle="0a87780b0c0bbb24180b37f4de3f306b"
MG_p_move="a42155d4da00b9046be1d05bfeeccce4"; MG_p_standup="c4ff16aacd41da74aa62b64218ae27f2"; MG_p_pdeath="6f01c977c48a82348b5e0da0cf4ac432"
# Machinegun - Combat (stand)
MG_draw="74b4990a37653764aabeae8bbe63f1cc"; MG_shoot="88301c2d7ce09d8489430c8f98412779"
MG_shoot_burst="112398df838422348b21c77a090a22b8"; MG_shoot_loop="89977e41ed1f34a4ab88c1a6687f9404"
MG_reload="35296f3a83c6e9a40b1e5c3e92436336"; MG_grenade="45096ab1344bb0d4b81df5f554a793bc"
MG_intA="81b19dc94dc7b474c91ef42375ae3c08"; MG_intB="68e7e93ad7416394e907404fb4fc7ba1"; MG_damage="2608b3e9884facb439ee8aee4cfca36e"
# Machinegun - Combat (crouch)
MG_c_draw="09d3c1707905eb34e9e3eb22cf622930"; MG_c_shoot="29ba05069f89f8c44aec1fdb41f2ae2e"
MG_c_shoot_burst="fa58be2fc38d2cb42a32a0caf719054a"; MG_c_shoot_loop="40afd0981fa8e3a498c21de8e8d6929e"
MG_c_reload="b2940bb50756dac4a8025eab695b5750"; MG_c_grenade="628248303f9641a4aa9b3f56aa3ee777"; MG_c_damage="0f5771b0eb739524dafdd663f15b6919"
# Machinegun - Combat (prone)
MG_p_draw="16100f59d6fb28641a1d5ccba6d14307"; MG_p_shoot="f6d049b28bfcc634b990bc40118deedb"
MG_p_shoot_burst="258ad03ce22106840a1668921fb13bbf"; MG_p_shoot_loop="0828ae25f1873d54385d19259f7afb52"
MG_p_reload="a0eafe0fc0022614592b2623cb63e7b8"; MG_p_grenade="64eecf2a695452046b1098048a5e4a0a"; MG_p_damage="94f7df8a92630f04998eea4dd59e2292"
# Machinegun - Deaths
MG_dA="935b2694b2f4d33459bfd9d4b328a857"; MG_dB="79222beb7a601334e819742cc7c9aba6"
MG_dC="eba1ccea6f8d96b459ea0deaf1c222b2"; MG_dD="6a1ca735ece5ba74c8300a7f80c0aa77"; MG_dE="d2b7b914b43e68f4089e9b872a5a533e"

# RocketLauncher - Standing
RL_idle="79f54940c30a0f24eaf5d4f461bcdef1"; RL_walk="2002f2d1104af7441980ab7057603bc3"
RL_walk_back="d6d3001e1e21265439bc2670ef1eb75e"; RL_walk_left="48a12cf0ac65a224989d5cc240d0d7e3"; RL_walk_right="06614879ad71d6d44ba36adb44c78a87"
RL_run="bbc5fba60b8fb8048ae87c7ba2b4f433"; RL_run_back="8d5d67b78083c6a47b6a96754bcc489b"
RL_run_left="0dd8d7e76bbf7e04b8a490fd0996768e"; RL_run_right="61b901517960a8f4fa6e571cc285d8f2"
RL_sprint="3e2e8ca2cba5d204792484818437ebac"; RL_roll="906c2cd693954e745a19b12ff75560ac"
RL_jump1="c883a15892123a04795f92bfd345ae7e"; RL_jump2="f66d2d183f69bd24c9a8f30cc26c5315"; RL_jump3="cb218e672cab8094a9f4fa237c106820"
RL_lad_up="bdd445da44619244db8e79dbea3adb03"; RL_lad_idle="76adcc4cf10463a4086bf0633d51d4f6"; RL_lad_down="f7f2ea7ba0cdb924bbfb2b6057a5ea8c"
# RocketLauncher - Crouch
RL_c_idle="5a7c9d4daf1ef724f83d8ed1ba6fdb30"; RL_c_walk="80d07075d9c8570428efdd687c28d75b"
RL_c_walk_back="1cb39cfe065747645bcd5d5967dc1afc"; RL_c_walk_left="fdb7f68d705f6f8468e98a7ab6bc4aa6"; RL_c_walk_right="c1158804db8e7934fbc6aaddd6a94549"
# RocketLauncher - Guard
RL_g_idle="63add3c36f6fdb3479bb9fab241401ce"; RL_g_walk="41a9f050195924147b6eeeb68652845c"; RL_g_run="4eb0664d907115143a76f3240c523913"
# RocketLauncher - Prone
RL_p_goto="e88ddf751e6f9b248b7e524012e56d6e"; RL_p_idle="0f5d9f34f58294340a24b16d2020a838"
RL_p_move="60485a6b3c6386241b7eb848deb54b88"; RL_p_standup="3bd445e11ac4b144d90a60c54b5194f3"; RL_p_pdeath="99e19cc10d490ce4fb872981f0d6174b"
# RocketLauncher - Combat (stand)
RL_draw="45f42b2ebd45dd442ad9a250e392a5e0"; RL_shoot="b778f175db1f4c246b6c839179ddbc66"
RL_reload="e222e803854496a4fa73510aec6d037f"; RL_grenade="4f00ea7b68ffe364e92cfad5f2befff8"
RL_intA="b75a17fb810a3f946baae847a7cd1dc7"; RL_intB="2028fe19826aff34a9602a034501a338"; RL_damage="b6a03492dc8613e4e95d05900ad0eb1c"
# RocketLauncher - Combat (crouch)
RL_c_draw="814eda90a50efbc489c363e0b78ce1aa"; RL_c_shoot="44836fbc85e5cab4dbc794665e5479f5"
RL_c_reload="3b4f042b983dba74aa48e7ea7910a0a9"; RL_c_grenade="8cc6089fd9f90b44f8e388dca2180a78"; RL_c_damage="710a30dbf74875542bf4fd27e2dbe8c0"
# RocketLauncher - Combat (prone)
RL_p_draw="f3cde16bfaf5ed24295769c43f4de426"; RL_p_shoot="c394b1369e3bff44e8bcc599f26adb92"
RL_p_reload="dc475356c6a905e4991c93e1a658f495"; RL_p_grenade="eae7e1d2ade0c8b409a33f1ddc50080b"; RL_p_damage="378eef9fe06893844880e74f9b8e2b0c"
# RocketLauncher - Deaths
RL_dA="ebc8b876e2f12e94585d2590c70717e9"; RL_dB="734b5a767b28a45428bba4c8cd1bb225"; RL_dC="12d3d34ff30737441825bd8d95a83e52"
}

# ---- WEAPON DEFINITIONS ----
# Each weapon: prefix, index, death count, has_shoot_burst, has_shoot_loop, has_shoot_bolt, has_shoot_shotgun, is_knife
$weapons = @(
  @{p="HG"; idx=0; dc=5; burst=1; loop=0; bolt=0; shotgun=0; knife=0; name="Handgun"}
  @{p="IN"; idx=1; dc=5; burst=1; loop=0; bolt=1; shotgun=1; knife=0; name="Infantry"}
  @{p="HV"; idx=2; dc=5; burst=1; loop=1; bolt=0; shotgun=0; knife=0; name="Heavy"}
  @{p="KN"; idx=3; dc=5; burst=0; loop=0; bolt=0; shotgun=0; knife=1; name="Knife"}
  @{p="MG"; idx=4; dc=5; burst=1; loop=1; bolt=0; shotgun=0; knife=0; name="Machinegun"}
  @{p="RL"; idx=5; dc=3; burst=0; loop=0; bolt=0; shotgun=0; knife=0; name="RocketLauncher"}
)

# ---- ID GENERATION ----
$script:idCounter = 9200000
function NID { $script:idCounter += 1; return $script:idCounter }

# ---- YAML BUILDER ----
$sb = [System.Text.StringBuilder]::new(500000)
function Y($s) { $sb.AppendLine($s) | Out-Null }

# ---- YAML BLOCK HELPERS ----
function BlendTree2D($id, $p, $q, $clips) {
    # clips = array of @{x=..;y=..;guid=..}
    # BlendType 3 = FreeformCartesian
    Y "--- !u!206 &$id"
    Y "BlendTree:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: Blend Tree"
    Y "  m_Childs:"
    foreach ($c in $clips) {
        $m = if ($c.guid) { $(Clip $c.guid) } else { $NC }
        Y "  - serializedVersion: 2"
        Y "    m_Motion: $m"
        Y "    m_Threshold: 0"
        Y "    m_Position: {x: $($c.x), y: $($c.y)}"
        Y "    m_TimeScale: 1"
        Y "    m_CycleOffset: 0"
        Y "    m_DirectBlendParameter: VelocityX"
        Y "    m_Mirror: 0"
    }
    Y "  m_BlendParameter: $p"
    Y "  m_BlendParameterY: $q"
    Y "  m_MinThreshold: -1"
    Y "  m_MaxThreshold: 1"
    Y "  m_UseAutomaticThresholds: 0"
    Y "  m_NormalizeBlendValues: 1"
    Y "  m_BlendType: 3"
    Y "  m_Controller: {fileID: 9100000}"
}

function BlendTree1D($id, $p, $clips) {
    # clips = array of @{t=threshold; guid=..}
    Y "--- !u!206 &$id"
    Y "BlendTree:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: Blend Tree"
    Y "  m_Childs:"
    foreach ($c in $clips) {
        $m = if ($c.guid) { $(Clip $c.guid) } else { $NC }
        Y "  - serializedVersion: 2"
        Y "    m_Motion: $m"
        Y "    m_Threshold: $($c.t)"
        Y "    m_Position: {x: 0, y: 0}"
        Y "    m_TimeScale: 1"
        Y "    m_CycleOffset: 0"
        Y "    m_DirectBlendParameter: Speed"
        Y "    m_Mirror: 0"
    }
    Y "  m_BlendParameter: $p"
    Y "  m_BlendParameterY: Speed"
    Y "  m_MinThreshold: 0"
    Y "  m_MaxThreshold: 2"
    Y "  m_UseAutomaticThresholds: 0"
    Y "  m_NormalizeBlendValues: 0"
    Y "  m_BlendType: 0"
    Y "  m_Controller: {fileID: 9100000}"
}

function State($id, $name, $motionGuid, $motionTree, $speed, $loop, $transitions, $x, $y) {
    $m = if ($motionTree) { "{fileID: $motionTree}" } elseif ($motionGuid) { $(Clip $motionGuid) } else { $NC }
    Y "--- !u!1102 &$id"
    Y "AnimatorState:"
    Y "  serializedVersion: 6"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: $name"
    Y "  m_Speed: $speed"
    Y "  m_CycleOffset: 0"
    Y "  m_Transitions:"
    foreach ($t in $transitions) { Y "  - {fileID: $t}" }
    Y "  m_StateMachineBehaviours: []"
    Y "  m_Position: {x: $x, y: $y, z: 0}"
    Y "  m_IKOnFeet: 0"
    Y "  m_WriteDefaultValues: 1"
    Y "  m_Mirror: 0"
    Y "  m_SpeedParameterActive: 0"
    Y "  m_MirrorParameterActive: 0"
    Y "  m_CycleOffsetParameterActive: 0"
    Y "  m_TimeParameterActive: 0"
    Y "  m_Motion: $m"
    Y "  m_Tag: "
    Y "  m_SpeedParameter: "
    Y "  m_MirrorParameter: "
    Y "  m_CycleOffsetParameter: "
    Y "  m_TimeParameter: "
}

function Transition($id, $dst, $isDst, $hasDur, $dur, $hasExit, $exitTime, $interrupt, $conds) {
    # isDst: 0=state, 1=SSM; conds=array of @{mode=;param=;thr=}
    Y "--- !u!1101 &$id"
    Y "AnimatorStateTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    $dstState = if ($isDst -eq 0) { "{fileID: $dst}" } else { "{fileID: 0}" }
    $dstSSM   = if ($isDst -eq 1) { "{fileID: $dst}" } else { "{fileID: 0}" }
    Y "  m_DstState: $dstState"
    Y "  m_DstStateMachine: $dstSSM"
    Y "  m_IsExit: 0"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
    Y "  m_Atomic: 1"
    Y "  m_TransitionDuration: $dur"
    Y "  m_TransitionOffset: 0"
    Y "  m_ExitTime: $exitTime"
    Y "  m_HasExitTime: $hasExit"
    Y "  m_HasFixedDuration: 1"
    Y "  m_InterruptionSource: $interrupt"
    Y "  m_OrderedInterruption: 1"
    Y "  m_CanTransitionToSelf: 1"
}

# ExitTransition: exits to Exit node at clip end (HasExitTime=1, exitTime=0.95)
# Used for one-shot animations (shoot, reload, draw, etc.)
function ExitTransition($id, $conds, $dur) {
    Y "--- !u!1101 &$id"
    Y "AnimatorStateTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    Y "  m_DstState: {fileID: 0}"
    Y "  m_DstStateMachine: {fileID: 0}"
    Y "  m_IsExit: 1"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
    Y "  m_Atomic: 1"
    Y "  m_TransitionDuration: $dur"
    Y "  m_TransitionOffset: 0"
    Y "  m_ExitTime: 0.95"
    Y "  m_HasExitTime: 1"
    Y "  m_HasFixedDuration: 1"
    Y "  m_InterruptionSource: 0"
    Y "  m_OrderedInterruption: 1"
    Y "  m_CanTransitionToSelf: 0"
}

# ExitTransitionCond: exits to Exit node based on CONDITION only (HasExitTime=0)
# Used for looping animations (ShootLoop) that exit when bool turns false
function ExitTransitionCond($id, $conds, $dur) {
    Y "--- !u!1101 &$id"
    Y "AnimatorStateTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    Y "  m_DstState: {fileID: 0}"
    Y "  m_DstStateMachine: {fileID: 0}"
    Y "  m_IsExit: 1"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
    Y "  m_Atomic: 1"
    Y "  m_TransitionDuration: $dur"
    Y "  m_TransitionOffset: 0"
    Y "  m_ExitTime: 0"
    Y "  m_HasExitTime: 0"
    Y "  m_HasFixedDuration: 1"
    Y "  m_InterruptionSource: 0"
    Y "  m_OrderedInterruption: 1"
    Y "  m_CanTransitionToSelf: 0"
}

# SSMTransition: AnimatorTransition (1109) from one SSM to another SSM (for weapon switch)
function SSMTransition($id, $dstSSM, $conds) {
    Y "--- !u!1109 &$id"
    Y "AnimatorTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    Y "  m_DstState: {fileID: 0}"
    Y "  m_DstStateMachine: {fileID: $dstSSM}"
    Y "  m_IsExit: 0"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
}

function EntryTransition($id, $dstState, $dstSSM, $conds) {
    # For root SSM entry to per-weapon SSMs
    Y "--- !u!1109 &$id"
    Y "AnimatorTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    $ds = if ($dstState) { "{fileID: $dstState}" } else { "{fileID: 0}" }
    $dm = if ($dstSSM)   { "{fileID: $dstSSM}"   } else { "{fileID: 0}" }
    Y "  m_DstState: $ds"
    Y "  m_DstStateMachine: $dm"
    Y "  m_IsExit: 0"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
}

function AnyStateTransition($id, $dstState, $conds, $hasDur, $dur, $hasExit, $exitTime) {
    Y "--- !u!1101 &$id"
    Y "AnimatorStateTransition:"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: "
    Y "  m_Conditions:"
    foreach ($c in $conds) {
        Y "  - m_ConditionMode: $($c.mode)"
        Y "    m_ConditionEvent: $($c.param)"
        Y "    m_EventTreshold: $($c.thr)"
    }
    Y "  m_DstState: {fileID: $dstState}"
    Y "  m_DstStateMachine: {fileID: 0}"
    Y "  m_IsExit: 0"
    Y "  m_Solo: 0"
    Y "  m_Mute: 0"
    Y "  m_Atomic: 1"
    Y "  m_TransitionDuration: $dur"
    Y "  m_TransitionOffset: 0"
    Y "  m_ExitTime: $exitTime"
    Y "  m_HasExitTime: $hasExit"
    Y "  m_HasFixedDuration: 1"
    Y "  m_InterruptionSource: 0"
    Y "  m_OrderedInterruption: 1"
    Y "  m_CanTransitionToSelf: 0"
}

# Condition helpers
function CTrue($param) { @{mode=1; param=$param; thr=0} }    # bool == true
function CFalse($param){ @{mode=2; param=$param; thr=0} }    # bool == false
function CGt($param,$v){ @{mode=3; param=$param; thr=$v} }   # float > v
function CLt($param,$v){ @{mode=4; param=$param; thr=$v} }   # float < v
function CEq($param,$v){ @{mode=6; param=$param; thr=$v} }   # int == v
function CNEq($param,$v){@{mode=7; param=$param; thr=$v} }   # int != v
function CTrig($param)  { @{mode=9; param=$param; thr=0} }   # trigger - NOTE: mode 9 is If (trigger)

# ---- SSM WRITER ----
function WriteSMHeader($id, $name, $defaultStateId, $childStates, $childSSMs, $anyTrans, $entryTrans, $smTrans, $entryX, $entryY, $anyX, $anyY) {
    Y "--- !u!1107 &$id"
    Y "AnimatorStateMachine:"
    Y "  serializedVersion: 6"
    Y "  m_ObjectHideFlags: 1"
    Y "  m_CorrespondingSourceObject: {fileID: 0}"
    Y "  m_PrefabInstance: {fileID: 0}"
    Y "  m_PrefabAsset: {fileID: 0}"
    Y "  m_Name: $name"
    Y "  m_ChildStates:"
    $xi=200; $yi=0
    foreach ($s in $childStates) {
        Y "  - serializedVersion: 1"
        Y "    m_State: {fileID: $($s.id)}"
        Y "    m_Position: {x: $($s.x), y: $($s.y), z: 0}"
        $xi+=200; $yi+=80
    }
    Y "  m_ChildStateMachines:"
    foreach ($m in $childSSMs) {
        Y "  - m_StateMachine: {fileID: $($m.id)}"
        Y "    m_Position: {x: $($m.x), y: $($m.y), z: 0}"
    }
    Y "  m_AnyStateTransitions:"
    foreach ($t in $anyTrans) { Y "  - {fileID: $t}" }
    Y "  m_EntryTransitions:"
    foreach ($t in $entryTrans) { Y "  - {fileID: $t}" }
    Y "  m_StateMachineTransitions:"
    # smTrans = hashtable: key=srcSSMFileID (int), value=array of transitionFileIDs
    if ($smTrans -and $smTrans.Count -gt 0) {
        foreach ($kv in $smTrans.GetEnumerator()) {
            $transArr = $kv.Value
            if ($transArr -and $transArr.Count -gt 0) {
                Y "    {fileID: $($kv.Key)}:"
                foreach ($tid in $transArr) { Y "    - {fileID: $tid}" }
            } else {
                Y "    {fileID: $($kv.Key)}: []"
            }
        }
    } else { Y "    {}" }
    Y "  m_StateMachineBehaviours: []"
    Y "  m_AnyStatePosition: {x: $anyX, y: $anyY, z: 0}"
    Y "  m_EntryPosition: {x: $entryX, y: $entryY, z: 0}"
    Y "  m_ExitPosition: {x: 800, y: $entryY, z: 0}"
    Y "  m_ParentStateMachinePosition: {x: 800, y: $anyY, z: 0}"
    $defRef = if ($defaultStateId) { "{fileID: $defaultStateId}" } else { "{fileID: 0}" }
    Y "  m_DefaultState: $defRef"
}

# ========================================================
# PER-WEAPON BASE LAYER SSM
# ========================================================
# Returns a hashtable with the SSM id and all generated state/transition IDs
function BuildBaseWeaponSSM($wp) {
    $p = $wp.p
    # --- allocate IDs ---
    $ssmId    = NID
    # States
    $sStandId = NID; $standTreeId = NID
    $sCrouchId= NID; $crouchTreeId= NID
    $sGuardId = NID; $guardTreeId = NID
    $sSprintId= NID
    $sRollId  = NID
    $sJ1Id    = NID; $sJ2Id = NID; $sJ3Id = NID
    $sLadUpId = NID; $sLadIdleId= NID; $sLadDownId= NID
    $sPGoToId = NID; $sPIdleId= NID; $sPMoveId= NID; $sPUpId= NID

    # --- Transitions (all will be referenced in state m_Transitions and in SSM) ---
    # Stand -> other
    $tStandToSprint = NID; $tStandToCrouch = NID; $tStandToGuard  = NID
    $tStandToRoll   = NID; $tStandToJump   = NID; $tStandToLadder = NID
    $tStandToProne  = NID
    # Sprint -> Stand
    $tSprintToStand = NID
    # Roll -> Stand
    $tRollToStand   = NID
    # Jump chain
    $tJ1ToJ2 = NID; $tJ2ToJ3 = NID; $tJ3ToStand = NID
    # Ladder
    $tLadIdleToUp = NID; $tLadIdleToDown= NID; $tLadUpToIdle= NID; $tLadDownToIdle= NID; $tLadToStand= NID
    # Crouch
    $tCrouchToStand = NID; $tCrouchToSprint= NID; $tCrouchToProne = NID
    # Guard
    $tGuardToStand  = NID; $tGuardToSprint = NID
    # Prone
    $tPGoToToIdle   = NID; $tPIdleToMove= NID; $tPMoveToIdle= NID; $tPIdleToUp= NID; $tPUpToStand= NID
    # Entry
    $tEntry = NID

    # ---- BLEND TREES ----
    # Stand locomotion 2D: idle(0,0) walk-fwd(0,1) walk-back(0,-1) walk-left(-1,0) walk-right(1,0)
    #                      run-fwd(0,2) run-back(0,-2) run-left(-2,0) run-right(2,0)
    BlendTree2D $standTreeId "VelocityX" "VelocityY" @(
        @{x=0; y=0;  guid=$G["${p}_idle"]}
        @{x=0; y=1;  guid=$G["${p}_walk"]}
        @{x=0; y=-1; guid=$G["${p}_walk_back"]}
        @{x=-1; y=0; guid=$G["${p}_walk_left"]}
        @{x=1; y=0;  guid=$G["${p}_walk_right"]}
        @{x=0; y=2;  guid=$G["${p}_run"]}
        @{x=0; y=-2; guid=$G["${p}_run_back"]}
        @{x=-2; y=0; guid=$G["${p}_run_left"]}
        @{x=2; y=0;  guid=$G["${p}_run_right"]}
    )
    # Crouch locomotion 2D
    BlendTree2D $crouchTreeId "VelocityX" "VelocityY" @(
        @{x=0;  y=0;  guid=$G["${p}_c_idle"]}
        @{x=0;  y=1;  guid=$G["${p}_c_walk"]}
        @{x=0;  y=-1; guid=$G["${p}_c_walk_back"]}
        @{x=-1; y=0;  guid=$G["${p}_c_walk_left"]}
        @{x=1;  y=0;  guid=$G["${p}_c_walk_right"]}
    )
    # Guard locomotion 1D: idle(0) walk(1) run(2)
    BlendTree1D $guardTreeId "Speed" @(
        @{t=0; guid=$G["${p}_g_idle"]}
        @{t=1; guid=$G["${p}_g_walk"]}
        @{t=2; guid=$G["${p}_g_run"]}
    )

    # ---- TRANSITIONS ----
    # Stand → Sprint (IsSprinting=true)
    Transition $tStandToSprint $sSprintId 0 1 0.1 0 0 0 @(,(CTrue "IsSprinting"))
    # Stand → Crouch (IsCrouching=true, !IsProne)
    Transition $tStandToCrouch $sCrouchId 0 1 0.15 0 0 0 @(,(CTrue "IsCrouching"))
    # Stand → Guard (IsGuard=true, !IsCrouching, !IsProne)
    Transition $tStandToGuard $sGuardId 0 1 0.12 0 0 0 @(CTrue "IsGuard"; CFalse "IsCrouching"; CFalse "IsProne")
    # Stand → Roll (Roll trigger)
    Transition $tStandToRoll $sRollId 0 1 0.05 0 0 0 @(,(CTrig "Roll"))
    # Stand → JumpStart (!IsGrounded)
    Transition $tStandToJump $sJ1Id 0 1 0.05 0 0 0 @(,(CFalse "IsGrounded"))
    # Stand → Ladder (IsOnLadder=true)
    Transition $tStandToLadder $sLadIdleId 0 1 0.15 0 0 0 @(,(CTrue "IsOnLadder"))
    # Stand → ProneGoTo (IsProne=true, !IsCrouching)
    Transition $tStandToProne $sPGoToId 0 1 0.1 0 0 0 @(CTrue "IsProne"; CFalse "IsCrouching")
    # Sprint → Stand (!IsSprinting)
    Transition $tSprintToStand $sStandId 0 1 0.15 0 0 0 @(,(CFalse "IsSprinting"))
    # Roll → Stand (exit time 0.9)
    Transition $tRollToStand $sStandId 0 1 0.1 1 0.9 0 @()
    # Jump chain
    Transition $tJ1ToJ2 $sJ2Id 0 1 0.1 1 0.7 0 @()
    Transition $tJ2ToJ3 $sJ3Id 0 1 0.1 0 0 0 @(,(CTrue "IsGrounded"))
    Transition $tJ3ToStand $sStandId 0 1 0.15 1 0.85 0 @()
    # Ladder
    Transition $tLadIdleToUp   $sLadUpId   0 1 0.1 0 0 0 @(,(CGt "VelocityY" 0.1))
    Transition $tLadIdleToDown $sLadDownId 0 1 0.1 0 0 0 @(,(CLt "VelocityY" -0.1))
    Transition $tLadUpToIdle   $sLadIdleId 0 1 0.1 0 0 0 @(,(CLt "VelocityY" 0.1))
    Transition $tLadDownToIdle $sLadIdleId 0 1 0.1 0 0 0 @(,(CGt "VelocityY" -0.1))
    Transition $tLadToStand    $sStandId   0 1 0.15 0 0 0 @(,(CFalse "IsOnLadder"))
    # Crouch → Stand (!IsCrouching)
    Transition $tCrouchToStand  $sStandId  0 1 0.15 0 0 0 @(,(CFalse "IsCrouching"))
    # Crouch → Sprint
    Transition $tCrouchToSprint $sSprintId 0 1 0.1  0 0 0 @(,(CTrue "IsSprinting"))
    # Crouch → ProneGoTo (IsProne=true)
    Transition $tCrouchToProne  $sPGoToId  0 1 0.1  0 0 0 @(,(CTrue "IsProne"))
    # Guard → Stand (!IsGuard)
    Transition $tGuardToStand  $sStandId  0 1 0.12 0 0 0 @(,(CFalse "IsGuard"))
    # Guard → Sprint
    Transition $tGuardToSprint $sSprintId 0 1 0.1  0 0 0 @(,(CTrue "IsSprinting"))
    # Prone chain
    Transition $tPGoToToIdle $sPIdleId 0 1 0.1 1 0.95 0 @()
    Transition $tPIdleToMove $sPMoveId 0 1 0.12 0 0 0 @(,(CGt "Speed" 0.1))
    Transition $tPMoveToIdle $sPIdleId 0 1 0.12 0 0 0 @(,(CLt "Speed" 0.1))
    Transition $tPIdleToUp   $sPUpId   0 1 0.1  0 0 0 @(,(CFalse "IsProne"))
    Transition $tPUpToStand  $sStandId 0 1 0.15 1 0.9 0 @()
    # Entry transition (unconditional – this is the default state of this SSM)
    EntryTransition $tEntry $sStandId 0 @()

    # ---- STATES ----
    State $sStandId "Stand_Locomotion"  $null $standTreeId  1 1 @($tStandToSprint,$tStandToCrouch,$tStandToGuard,$tStandToRoll,$tStandToJump,$tStandToLadder,$tStandToProne) 200 100
    State $sCrouchId "Crouch_Locomotion" $null $crouchTreeId 1 1 @($tCrouchToStand,$tCrouchToSprint,$tCrouchToProne) 200 300
    State $sGuardId "Guard_Locomotion"  $null $guardTreeId   1 1 @($tGuardToStand,$tGuardToSprint) 500 100
    State $sSprintId "Sprint" $G["${p}_sprint"] $null        1 1 @($tSprintToStand) 500 -100
    State $sRollId "Roll"   $G["${p}_roll"]   $null          1 0 @($tRollToStand) 800 0
    State $sJ1Id "Jump_Start" $G["${p}_jump1"] $null         1 0 @($tJ1ToJ2) 800 -200
    State $sJ2Id "Jump_Air"   $G["${p}_jump2"] $null         1 1 @($tJ2ToJ3) 1000 -200
    State $sJ3Id "Jump_Land"  $G["${p}_jump3"] $null         1 0 @($tJ3ToStand) 1200 -200
    State $sLadUpId   "Ladder_Up"   $G["${p}_lad_up"]   $null 1 1 @($tLadUpToIdle) 800 200
    State $sLadIdleId "Ladder_Idle" $G["${p}_lad_idle"] $null 1 1 @($tLadIdleToUp,$tLadIdleToDown,$tLadToStand) 1000 200
    State $sLadDownId "Ladder_Down" $G["${p}_lad_down"] $null 1 1 @($tLadDownToIdle) 1200 200
    State $sPGoToId "Prone_GoTo"   $G["${p}_p_goto"]   $null  1 0 @($tPGoToToIdle) 200 500
    State $sPIdleId "Prone_Idle"   $G["${p}_p_idle"]   $null  1 1 @($tPIdleToMove,$tPIdleToUp) 400 500
    State $sPMoveId "Prone_Move"   $G["${p}_p_move"]   $null  1 1 @($tPMoveToIdle) 600 500
    State $sPUpId   "Prone_StandUp" $G["${p}_p_standup"] $null 1 0 @($tPUpToStand) 800 500

    # ---- SSM ----
    $childStatesArr = @(
        @{id=$sStandId; x=200; y=100}; @{id=$sCrouchId; x=200; y=300}
        @{id=$sGuardId; x=500; y=100}; @{id=$sSprintId; x=500; y=-100}
        @{id=$sRollId;  x=800; y=0};   @{id=$sJ1Id; x=800; y=-200}
        @{id=$sJ2Id; x=1000; y=-200};  @{id=$sJ3Id; x=1200; y=-200}
        @{id=$sLadUpId; x=800; y=200}; @{id=$sLadIdleId; x=1000; y=200}
        @{id=$sLadDownId; x=1200; y=200}
        @{id=$sPGoToId; x=200; y=500}; @{id=$sPIdleId; x=400; y=500}
        @{id=$sPMoveId; x=600; y=500}; @{id=$sPUpId; x=800; y=500}
    )
    WriteSMHeader $ssmId "$($wp.name)_Base" $sStandId $childStatesArr @() @() @($tEntry) @{} 50 20 50 120

    return @{ssmId=$ssmId; defaultStateId=$sStandId}
}

# ========================================================
# PER-WEAPON UPPER BODY SSM
# ========================================================
function BuildUBWeaponSSM($wp) {
    $p = $wp.p
    $ssmId = NID

    # Helper: build one combat state with exit-to-empty transition
    function CombatState($guid, $dur, $loop) {
        $sid = NID; $exitTrans = NID
        ExitTransition $exitTrans @() $dur
        State $sid $null $guid $null 1 $loop @($exitTrans) (NID%1000) (NID%800)
        return @{sid=$sid; exitT=$exitTrans}
    }

    # Empty default state (no motion – base layer shows through)
    $sEmptyId = NID
    State $sEmptyId "UB_Empty" $null $null 1 1 @() 50 20

    # --- STAND COMBAT STATES ---
    $sDraw_s  = NID; $tDraw_s_ex  = NID; ExitTransition $tDraw_s_ex  @() 0.1; State $sDraw_s  "Draw_Stand"   $G["${p}_draw"]        $null 1 0 @($tDraw_s_ex)  200 -100
    $sShoot_s = NID; $tShoot_s_ex = NID; ExitTransition $tShoot_s_ex @() 0.05;State $sShoot_s "Shoot_Stand"  $G["${p}_shoot"]       $null 1 0 @($tShoot_s_ex) 400 -100
    $sRld_s   = NID; $tRld_s_ex   = NID; ExitTransition $tRld_s_ex   @() 0.1; State $sRld_s   "Reload_Stand" $G["${p}_reload"]      $null 1 0 @($tRld_s_ex)   600 -100
    $sGrn_s   = NID; $tGrn_s_ex   = NID; ExitTransition $tGrn_s_ex   @() 0.1; State $sGrn_s   "Grenade_Stand" $G["${p}_grenade"]   $null 1 0 @($tGrn_s_ex)   800 -100
    $sIntA_s  = NID; $tIntA_s_ex  = NID; ExitTransition $tIntA_s_ex  @() 0.1; State $sIntA_s  "Interact_A"   $G["${p}_intA"]       $null 1 0 @($tIntA_s_ex)  1000 -100
    $sIntB_s  = NID; $tIntB_s_ex  = NID; ExitTransition $tIntB_s_ex  @() 0.1; State $sIntB_s  "Interact_B"   $G["${p}_intB"]       $null 1 0 @($tIntB_s_ex)  1200 -100
    $sDmg_s   = NID; $tDmg_s_ex   = NID; ExitTransition $tDmg_s_ex   @() 0.05;State $sDmg_s   "Damage_Stand" $G["${p}_damage"]     $null 1 0 @($tDmg_s_ex)   1400 -100

    # Burst (if applicable)
    $sBurst_s = 0; $tBurst_s_ex = 0
    if ($wp.burst) {
        $sBurst_s = NID; $tBurst_s_ex = NID
        ExitTransition $tBurst_s_ex @() 0.05
        State $sBurst_s "ShootBurst_Stand" $G["${p}_shoot_burst"] $null 1 0 @($tBurst_s_ex) 400 -200
    }
    # Shoot Loop (Heavy/Machinegun) — condition-based exit when ShootLoop=false
    $sLoop_s = 0
    if ($wp.loop) {
        $sLoop_s = NID; $tLoop_s_ex = NID
        ExitTransitionCond $tLoop_s_ex @(,(CFalse "ShootLoop")) 0.05
        State $sLoop_s "ShootLoop_Stand" $G["${p}_shoot_loop"] $null 1 1 @($tLoop_s_ex) 400 -300
    }
    # Shoot Bolt (Infantry)
    $sBolt_s = 0
    if ($wp.bolt) {
        $sBolt_s = NID; $tBolt_s_ex = NID
        ExitTransition $tBolt_s_ex @() 0.05
        State $sBolt_s "ShootBolt_Stand" $G["${p}_shoot_bolt"] $null 1 0 @($tBolt_s_ex) 400 -300
    }
    # Shoot Shotgun (Infantry)
    $sShotgun_s = 0
    if ($wp.shotgun) {
        $sShotgun_s = NID; $tShotgun_s_ex = NID
        ExitTransition $tShotgun_s_ex @() 0.05
        State $sShotgun_s "ShootShotgun_Stand" $G["${p}_shoot_shotgun"] $null 1 0 @($tShotgun_s_ex) 400 -400
    }
    # Knife attacks
    $sAttackA_s = 0; $sAttackB_s = 0
    if ($wp.knife) {
        $sAttackA_s = NID; $tAttA_s_ex = NID; ExitTransition $tAttA_s_ex @() 0.08; State $sAttackA_s "Attack_A_Stand" $G["${p}_attackA"] $null 1 0 @($tAttA_s_ex) 400 -100
        $sAttackB_s = NID; $tAttB_s_ex = NID; ExitTransition $tAttB_s_ex @() 0.08; State $sAttackB_s "Attack_B_Stand" $G["${p}_attackB"] $null 1 0 @($tAttB_s_ex) 600 -100
    }

    # --- CROUCH COMBAT STATES ---
    $sDraw_c  = NID; $tDraw_c_ex  = NID; ExitTransition $tDraw_c_ex  @() 0.1; State $sDraw_c  "Draw_Crouch"   $G["${p}_c_draw"]   $null 1 0 @($tDraw_c_ex)  200 100
    $sShoot_c = NID; $tShoot_c_ex = NID; ExitTransition $tShoot_c_ex @() 0.05;State $sShoot_c "Shoot_Crouch"  $G["${p}_c_shoot"]  $null 1 0 @($tShoot_c_ex) 400 100
    $sRld_c   = NID; $tRld_c_ex   = NID; ExitTransition $tRld_c_ex   @() 0.1; State $sRld_c   "Reload_Crouch" $G["${p}_c_reload"] $null 1 0 @($tRld_c_ex)   600 100
    $sGrn_c   = NID; $tGrn_c_ex   = NID; ExitTransition $tGrn_c_ex   @() 0.1; State $sGrn_c   "Grenade_Crouch" $G["${p}_c_grenade"] $null 1 0 @($tGrn_c_ex) 800 100
    $sDmg_c   = NID; $tDmg_c_ex   = NID; ExitTransition $tDmg_c_ex   @() 0.05;State $sDmg_c   "Damage_Crouch" $G["${p}_c_damage"] $null 1 0 @($tDmg_c_ex)  1000 100
    $sBurst_c = 0
    if ($wp.burst) {
        $sBurst_c = NID; $tBurst_c_ex = NID; ExitTransition $tBurst_c_ex @() 0.05; State $sBurst_c "ShootBurst_Crouch" $G["${p}_c_shoot_burst"] $null 1 0 @($tBurst_c_ex) 400 200
    }
    $sLoop_c = 0
    if ($wp.loop) {
        $sLoop_c = NID; $tLoop_c_ex = NID; ExitTransitionCond $tLoop_c_ex @(,(CFalse "ShootLoop")) 0.05; State $sLoop_c "ShootLoop_Crouch" $G["${p}_c_shoot_loop"] $null 1 1 @($tLoop_c_ex) 400 300
    }
    $sAttackA_c = 0; $sAttackB_c = 0
    if ($wp.knife) {
        $sAttackA_c = NID; $tAttA_c_ex = NID; ExitTransition $tAttA_c_ex @() 0.08; State $sAttackA_c "Attack_A_Crouch" $G["${p}_c_attackA"] $null 1 0 @($tAttA_c_ex) 400 100
        $sAttackB_c = NID; $tAttB_c_ex = NID; ExitTransition $tAttB_c_ex @() 0.08; State $sAttackB_c "Attack_B_Crouch" $G["${p}_c_attackB"] $null 1 0 @($tAttB_c_ex) 600 100
    }

    # --- PRONE COMBAT STATES ---
    $sDraw_p  = NID; $tDraw_p_ex  = NID; ExitTransition $tDraw_p_ex  @() 0.1; State $sDraw_p  "Draw_Prone"   $G["${p}_p_draw"]   $null 1 0 @($tDraw_p_ex)  200 300
    $sShoot_p = NID; $tShoot_p_ex = NID; ExitTransition $tShoot_p_ex @() 0.05;State $sShoot_p "Shoot_Prone"  $G["${p}_p_shoot"]  $null 1 0 @($tShoot_p_ex) 400 300
    $sRld_p   = NID; $tRld_p_ex   = NID; ExitTransition $tRld_p_ex   @() 0.1; State $sRld_p   "Reload_Prone" $G["${p}_p_reload"] $null 1 0 @($tRld_p_ex)   600 300
    $sGrn_p   = NID; $tGrn_p_ex   = NID; ExitTransition $tGrn_p_ex   @() 0.1; State $sGrn_p   "Grenade_Prone" $G["${p}_p_grenade"] $null 1 0 @($tGrn_p_ex) 800 300
    $sDmg_p   = NID; $tDmg_p_ex   = NID; ExitTransition $tDmg_p_ex   @() 0.05;State $sDmg_p   "Damage_Prone" $G["${p}_p_damage"] $null 1 0 @($tDmg_p_ex)  1000 300
    $sBurst_p = 0
    if ($wp.burst) {
        $sBurst_p = NID; $tBurst_p_ex = NID; ExitTransition $tBurst_p_ex @() 0.05; State $sBurst_p "ShootBurst_Prone" $G["${p}_p_shoot_burst"] $null 1 0 @($tBurst_p_ex) 400 400
    }
    $sLoop_p = 0
    if ($wp.loop) {
        $sLoop_p = NID; $tLoop_p_ex = NID; ExitTransitionCond $tLoop_p_ex @(,(CFalse "ShootLoop")) 0.05; State $sLoop_p "ShootLoop_Prone" $G["${p}_p_shoot_loop"] $null 1 1 @($tLoop_p_ex) 400 500
    }
    $sAttack_p = 0
    if ($wp.knife) {
        $sAttack_p = NID; $tAtt_p_ex = NID; ExitTransition $tAtt_p_ex @() 0.08; State $sAttack_p "Attack_Prone" $G["${p}_p_attack"] $null 1 0 @($tAtt_p_ex) 400 300
    }

    # ---- AnyState Transitions ----
    # For each combat action, 3 anystate transitions: !crouch!prone, crouch, prone
    # Also we don't allow re-entry into itself (canTransitionToSelf=0)
    $anyTrans = @()

    function MkAny($dst, $conds, $dur) {
        $tid = NID
        AnyStateTransition $tid $dst $conds 1 $dur 0 0
        $script:anyTrans += $tid
    }

    # Draw
    MkAny $sDraw_s  @(CTrig "Draw"; CFalse "IsCrouching"; CFalse "IsProne") 0.1
    MkAny $sDraw_c  @(CTrig "Draw"; CTrue  "IsCrouching")                   0.1
    MkAny $sDraw_p  @(CTrig "Draw"; CTrue  "IsProne")                       0.1

    # Shoot
    MkAny $sShoot_s @(CTrig "Shoot"; CFalse "IsCrouching"; CFalse "IsProne") 0.05
    MkAny $sShoot_c @(CTrig "Shoot"; CTrue  "IsCrouching")                   0.05
    MkAny $sShoot_p @(CTrig "Shoot"; CTrue  "IsProne")                       0.05

    if ($sBurst_s) {
        MkAny $sBurst_s @(CTrig "ShootBurst"; CFalse "IsCrouching"; CFalse "IsProne") 0.05
        MkAny $sBurst_c @(CTrig "ShootBurst"; CTrue  "IsCrouching")                   0.05
        MkAny $sBurst_p @(CTrig "ShootBurst"; CTrue  "IsProne")                       0.05
    }
    if ($sLoop_s) {
        MkAny $sLoop_s @(CTrue "ShootLoop"; CFalse "IsCrouching"; CFalse "IsProne") 0.05
        MkAny $sLoop_c @(CTrue "ShootLoop"; CTrue  "IsCrouching")                   0.05
        MkAny $sLoop_p @(CTrue "ShootLoop"; CTrue  "IsProne")                       0.05
    }
    if ($sBolt_s)    { MkAny $sBolt_s   @(CTrue "ShootBolt"; CFalse "IsCrouching"; CFalse "IsProne") 0.05 }
    if ($sShotgun_s) { MkAny $sShotgun_s @(CTrue "ShootShotgun"; CFalse "IsCrouching"; CFalse "IsProne") 0.05 }

    if ($wp.knife) {
        MkAny $sAttackA_s @(CTrig "Attack"; CEq "AttackIndex" 0; CFalse "IsCrouching"; CFalse "IsProne") 0.05
        MkAny $sAttackB_s @(CTrig "Attack"; CEq "AttackIndex" 1; CFalse "IsCrouching"; CFalse "IsProne") 0.05
        MkAny $sAttackA_c @(CTrig "Attack"; CEq "AttackIndex" 0; CTrue  "IsCrouching") 0.05
        MkAny $sAttackB_c @(CTrig "Attack"; CEq "AttackIndex" 1; CTrue  "IsCrouching") 0.05
        MkAny $sAttack_p  @(CTrig "Attack"; CTrue "IsProne") 0.05
    }

    # Reload
    MkAny $sRld_s @(CTrig "Reload"; CFalse "IsCrouching"; CFalse "IsProne") 0.1
    MkAny $sRld_c @(CTrig "Reload"; CTrue  "IsCrouching")                   0.1
    MkAny $sRld_p @(CTrig "Reload"; CTrue  "IsProne")                       0.1

    # Grenade
    MkAny $sGrn_s @(CTrig "ThrowGrenade"; CFalse "IsCrouching"; CFalse "IsProne") 0.1
    MkAny $sGrn_c @(CTrig "ThrowGrenade"; CTrue  "IsCrouching")                   0.1
    MkAny $sGrn_p @(CTrig "ThrowGrenade"; CTrue  "IsProne")                       0.1

    # Interact
    MkAny $sIntA_s @(CTrig "Interact"; CEq "InteractIndex" 0) 0.1
    MkAny $sIntB_s @(CTrig "Interact"; CEq "InteractIndex" 1) 0.1

    # TakeDamage
    MkAny $sDmg_s @(CTrig "TakeDamage"; CFalse "IsCrouching"; CFalse "IsProne") 0.05
    MkAny $sDmg_c @(CTrig "TakeDamage"; CTrue  "IsCrouching")                   0.05
    MkAny $sDmg_p @(CTrig "TakeDamage"; CTrue  "IsProne")                       0.05

    # Entry
    $tEntry = NID
    EntryTransition $tEntry $sEmptyId 0 @()

    # Collect all states for SSM
    $allStates = @(@{id=$sEmptyId;x=50;y=20})
    foreach ($s in @($sDraw_s,$sShoot_s,$sRld_s,$sGrn_s,$sIntA_s,$sIntB_s,$sDmg_s)) {
        if ($s) { $allStates += @{id=$s;x=200;y=-100} }
    }
    foreach ($s in @($sDraw_c,$sShoot_c,$sRld_c,$sGrn_c,$sDmg_c)) {
        if ($s) { $allStates += @{id=$s;x=200;y=100} }
    }
    foreach ($s in @($sDraw_p,$sShoot_p,$sRld_p,$sGrn_p,$sDmg_p)) {
        if ($s) { $allStates += @{id=$s;x=200;y=300} }
    }
    foreach ($s in @($sBurst_s,$sBurst_c,$sBurst_p,$sLoop_s,$sLoop_c,$sLoop_p,$sBolt_s,$sShotgun_s,$sAttackA_s,$sAttackB_s,$sAttackA_c,$sAttackB_c,$sAttack_p)) {
        if ($s) { $allStates += @{id=$s;x=600;y=-200} }
    }

    WriteSMHeader $ssmId "$($wp.name)_UpperBody" $sEmptyId $allStates @() $anyTrans @($tEntry) @{} 50 20 50 -50

    return @{ssmId=$ssmId}
}

# ========================================================
# PER-WEAPON DEATH SSM  (Death Layer - Full body override)
# Architecture:
#   - Death_Empty: default state (no clip, weight passes through)
#   - Death_A..E + Death_Prone: hold at last frame (loop=0, no auto-exit)
#   - AnyState → Death_X: when Die trigger + DeathIndex matches (weapon-specific clips)
#   - AnyState → Death_Empty: when Respawn trigger (exits death, lets player act again)
# ========================================================
function BuildDeathWeaponSSM($wp) {
    $p = $wp.p
    $ssmId = NID
    $dc = $wp.dc
    $sEmpty = NID
    State $sEmpty "Death_Empty" $null $null 1 1 @() 50 20

    $deathSuffix = @("A","B","C","D","E")
    $deathStates = @()
    $anyTrans = @()

    for ($i=0; $i -lt $dc; $i++) {
        $suf = $deathSuffix[$i]
        $gk = "${p}_d$suf"
        $sid = NID
        # Death states: loop=0 (play once), NO transitions out (hold last frame until Respawn)
        State $sid "Death_$suf" $G[$gk] $null 1 0 @() (200+$i*200) 100
        $deathStates += @{id=$sid; idx=$i; x=(200+$i*200); y=100}
        # AnyState → this death state: Die trigger + correct DeathIndex + not prone
        $atID = NID
        AnyStateTransition $atID $sid @(CTrig "Die"; CEq "DeathIndex" $i; CFalse "IsProne") 1 0.1 0 0
        $anyTrans += $atID
    }
    # Prone death — triggered when IsProne=true at moment of death
    $sProneD = NID
    State $sProneD "Death_Prone" $G["${p}_p_pdeath"] $null 1 0 @() (200+$dc*200) 100
    $atProneD = NID
    AnyStateTransition $atProneD $sProneD @(CTrig "Die"; CTrue "IsProne") 1 0.1 0 0
    $anyTrans += $atProneD

    # RESPAWN: AnyState → Death_Empty when Respawn trigger (unblocks Base+UB layers)
    $atRespawn = NID
    AnyStateTransition $atRespawn $sEmpty @(CTrig "Respawn") 1 0.15 0 0
    $anyTrans += $atRespawn

    $tEntry = NID
    EntryTransition $tEntry $sEmpty 0 @()

    $allStates = @(@{id=$sEmpty;x=50;y=20}) + $deathStates + @(@{id=$sProneD;x=(200+$dc*200);y=100})

    WriteSMHeader $ssmId "$($wp.name)_Death" $sEmpty $allStates @() $anyTrans @($tEntry) @{} 50 20 50 -50

    return @{ssmId=$ssmId}
}

# ========================================================
# BUILD ROOT SSMs
# Architecture fix:
#   1. Entry transitions: conditional first (WeaponType==X), default LAST
#   2. StateMachine transitions: from each weapon SSM to all others
#      so WeaponType change at runtime triggers immediate SSM switch
# ========================================================
function BuildRootSSM($name, $childSSMs) {
    $ssmId = NID
    $entryTrans = @()
    $smTrans = @{}   # key=srcSSMId, value=array of SMTransition IDs

    # ---- Entry transitions (conditional first, default LAST) ----
    foreach ($c in $childSSMs) {
        $etid = NID
        EntryTransition $etid 0 $c.ssmId @(,(CEq "WeaponType" $c.idx))
        $entryTrans += $etid
    }
    # Default entry — NO CONDITION — must be LAST so conditionals above take priority
    $defEntry = NID
    EntryTransition $defEntry 0 $childSSMs[0].ssmId @()
    $entryTrans += $defEntry

    # ---- StateMachine Transitions: each SSM -> every other SSM on WeaponType change ----
    # This is the key fix: allows runtime weapon switch to immediately re-route to correct SSM
    foreach ($src in $childSSMs) {
        $smTransList = @()
        foreach ($dst in $childSSMs) {
            if ($src.ssmId -ne $dst.ssmId) {
                $stid = NID
                SSMTransition $stid $dst.ssmId @(,(CEq "WeaponType" $dst.idx))
                $smTransList += $stid
            }
        }
        $smTrans[$src.ssmId] = $smTransList
    }

    $childSSMsArr = @()
    $xi = 200; $yi = 0
    foreach ($c in $childSSMs) {
        $childSSMsArr += @{id=$c.ssmId; x=$xi; y=$yi}
        $yi += 150
    }

    WriteSMHeader $ssmId $name 0 @() $childSSMsArr @() $entryTrans $smTrans 50 20 50 -50
    return $ssmId
}

# ========================================================
# PARAMETER WRITER
# ========================================================
function WriteParams {
    # float
    foreach ($fp in @("VelocityX","VelocityY","Speed")) {
        Y "  - m_Name: $fp"
        Y "    m_Type: 1"
        Y "    m_DefaultFloat: 0"
        Y "    m_DefaultInt: 0"
        Y "    m_DefaultBool: 0"
        Y "    m_Controller: {fileID: 9100000}"
    }
    # int
    foreach ($ip in @("WeaponType","DeathIndex","InteractIndex","AttackIndex")) {
        Y "  - m_Name: $ip"
        Y "    m_Type: 3"
        Y "    m_DefaultFloat: 0"
        Y "    m_DefaultInt: 0"
        Y "    m_DefaultBool: 0"
        Y "    m_Controller: {fileID: 9100000}"
    }
    # bool
    foreach ($bp in @("IsCrouching","IsProne","IsGuard","IsSprinting","IsGrounded","IsOnLadder","ShootLoop","ShootBolt","ShootShotgun")) {
        Y "  - m_Name: $bp"
        Y "    m_Type: 4"
        Y "    m_DefaultFloat: 0"
        Y "    m_DefaultInt: 0"
        Y "    m_DefaultBool: $(if ($bp -eq 'IsGrounded') { 1 } else { 0 })"
        Y "    m_Controller: {fileID: 9100000}"
    }
    # trigger — includes Respawn for exiting Death layer
    foreach ($tp in @("Shoot","ShootBurst","Reload","Draw","ThrowGrenade","Interact","TakeDamage","Attack","Die","Roll","Respawn")) {
        Y "  - m_Name: $tp"
        Y "    m_Type: 9"
        Y "    m_DefaultFloat: 0"
        Y "    m_DefaultInt: 0"
        Y "    m_DefaultBool: 0"
        Y "    m_Controller: {fileID: 9100000}"
    }
}

# ========================================================
# MAIN BUILD
# ========================================================
Write-Host "Building animator controller..."

# --- PRE-BUILD all weapon SSMs (writes YAML as side effect) ---
$baseSSMRefs = @(); $ubSSMRefs = @(); $deathSSMRefs = @()
foreach ($wp in $weapons) {
    Write-Host "  Building $($wp.name) Base SSM..."
    $b = BuildBaseWeaponSSM $wp
    $baseSSMRefs += @{ssmId=$b.ssmId; idx=$wp.idx}

    Write-Host "  Building $($wp.name) UpperBody SSM..."
    $u = BuildUBWeaponSSM $wp
    $ubSSMRefs += @{ssmId=$u.ssmId; idx=$wp.idx}

    Write-Host "  Building $($wp.name) Death SSM..."
    $d = BuildDeathWeaponSSM $wp
    $deathSSMRefs += @{ssmId=$d.ssmId; idx=$wp.idx}
}

# --- BUILD ROOT SSMs ---
$baseRootId  = BuildRootSSM "Base Layer"   $baseSSMRefs
$ubRootId    = BuildRootSSM "UpperBody"    $ubSSMRefs
$deathRootId = BuildRootSSM "Death"        $deathSSMRefs

# ========================================================
# ASSEMBLE FINAL YAML (header goes FIRST)
# ========================================================
$header = [System.Text.StringBuilder]::new(5000)
function H($s) { $header.AppendLine($s) | Out-Null }

H "%YAML 1.1"
H "%TAG !u! tag:unity3d.com,2011:"
H "--- !u!91 &9100000"
H "AnimatorController:"
H "  m_ObjectHideFlags: 0"
H "  m_CorrespondingSourceObject: {fileID: 0}"
H "  m_PrefabInstance: {fileID: 0}"
H "  m_PrefabAsset: {fileID: 0}"
H "  m_Name: SoldierAnimatorController"
H "  serializedVersion: 5"
H "  m_AnimatorParameters:"
WriteParams  # writes into $sb but we need it in header - re-route:
# Actually params go inside m_AnimatorParameters block in the controller object
# Let me build params string separately
$paramSb = [System.Text.StringBuilder]::new(2000)
function HP($s) { $paramSb.AppendLine($s) | Out-Null }
# float
foreach ($fp in @("VelocityX","VelocityY","Speed")) {
    HP "  - m_Name: $fp"; HP "    m_Type: 1"; HP "    m_DefaultFloat: 0"; HP "    m_DefaultInt: 0"; HP "    m_DefaultBool: 0"; HP "    m_Controller: {fileID: 9100000}"
}
# int
foreach ($ip in @("WeaponType","DeathIndex","InteractIndex","AttackIndex")) {
    HP "  - m_Name: $ip"; HP "    m_Type: 3"; HP "    m_DefaultFloat: 0"; HP "    m_DefaultInt: 0"; HP "    m_DefaultBool: 0"; HP "    m_Controller: {fileID: 9100000}"
}
# bool
foreach ($bp in @("IsCrouching","IsProne","IsGuard","IsSprinting","IsGrounded","IsOnLadder","ShootLoop","ShootBolt","ShootShotgun")) {
    $defB = if ($bp -eq 'IsGrounded') { 1 } else { 0 }
    HP "  - m_Name: $bp"; HP "    m_Type: 4"; HP "    m_DefaultFloat: 0"; HP "    m_DefaultInt: 0"; HP "    m_DefaultBool: $defB"; HP "    m_Controller: {fileID: 9100000}"
}
# trigger — includes Respawn
foreach ($tp in @("Shoot","ShootBurst","Reload","Draw","ThrowGrenade","Interact","TakeDamage","Attack","Die","Roll","Respawn")) {
    HP "  - m_Name: $tp"; HP "    m_Type: 9"; HP "    m_DefaultFloat: 0"; HP "    m_DefaultInt: 0"; HP "    m_DefaultBool: 0"; HP "    m_Controller: {fileID: 9100000}"
}

$finalHeader = [System.Text.StringBuilder]::new(5000)
function FH($s) { $finalHeader.AppendLine($s) | Out-Null }
FH "%YAML 1.1"
FH "%TAG !u! tag:unity3d.com,2011:"
FH "--- !u!91 &9100000"
FH "AnimatorController:"
FH "  m_ObjectHideFlags: 0"
FH "  m_CorrespondingSourceObject: {fileID: 0}"
FH "  m_PrefabInstance: {fileID: 0}"
FH "  m_PrefabAsset: {fileID: 0}"
FH "  m_Name: SoldierAnimatorController"
FH "  serializedVersion: 5"
FH "  m_AnimatorParameters:"
FH $paramSb.ToString().TrimEnd()
FH "  m_AnimatorLayers:"
# Layer 0: Base
FH "  - serializedVersion: 5"
FH "    m_Name: Base Layer"
FH "    m_StateMachine: {fileID: $baseRootId}"
FH "    m_Mask: {fileID: 0}"
FH "    m_Motions: []"
FH "    m_Behaviours: []"
FH "    m_BlendingMode: 0"
FH "    m_SyncedLayerIndex: -1"
FH "    m_DefaultWeight: 1"
FH "    m_IKPass: 0"
FH "    m_SyncedLayerAffectsTiming: 0"
FH "    m_Controller: {fileID: 9100000}"
# Layer 1: UpperBody (Avatar Mask optional - set to 0 for now)
FH "  - serializedVersion: 5"
FH "    m_Name: UpperBody"
FH "    m_StateMachine: {fileID: $ubRootId}"
FH "    m_Mask: {fileID: 0}"
FH "    m_Motions: []"
FH "    m_Behaviours: []"
FH "    m_BlendingMode: 0"
FH "    m_SyncedLayerIndex: -1"
FH "    m_DefaultWeight: 1"
FH "    m_IKPass: 0"
FH "    m_SyncedLayerAffectsTiming: 0"
FH "    m_Controller: {fileID: 9100000}"
# Layer 2: Death (full body override, highest priority)
FH "  - serializedVersion: 5"
FH "    m_Name: Death"
FH "    m_StateMachine: {fileID: $deathRootId}"
FH "    m_Mask: {fileID: 0}"
FH "    m_Motions: []"
FH "    m_Behaviours: []"
FH "    m_BlendingMode: 0"
FH "    m_SyncedLayerIndex: -1"
FH "    m_DefaultWeight: 1"
FH "    m_IKPass: 0"
FH "    m_SyncedLayerAffectsTiming: 0"
FH "    m_Controller: {fileID: 9100000}"

# Combine header + body
$output = $finalHeader.ToString() + $sb.ToString()
[System.IO.File]::WriteAllText($outputPath, $output, [System.Text.Encoding]::UTF8)

Write-Host "Done! Written to: $outputPath"
Write-Host "File size: $([System.IO.FileInfo]::new($outputPath).Length / 1KB) KB"

# Write meta file
$metaPath = $outputPath + ".meta"
$metaGuid = [guid]::NewGuid().ToString("N")
$metaContent = @"
fileFormatVersion: 2
guid: $metaGuid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 9100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
[System.IO.File]::WriteAllText($metaPath, $metaContent, [System.Text.Encoding]::UTF8)
Write-Host "Meta written: $metaPath (guid: $metaGuid)"
