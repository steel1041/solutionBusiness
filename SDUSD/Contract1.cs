using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;
using System.ComponentModel;
using Neo.SmartContract.Framework.Services.System;

namespace SDUSDContract
{
    public class Contract1 : SmartContract
    {

        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AQdP56hHfo54JCWfpPw4MXviJDtQJMtXFa");


        [Appcall("e4460377f6f8398170e6dea4646027ac9d117597")] //JumpCenter ScriptHash
        public static extern object SDTContract(string method, object[] args);

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;


        //nep5 func
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string Name()
        {
            return "Special Drawing USD";
        }
        public static string Symbol()
        {
            return "SDUSD";
        }

        public static byte Decimals()
        {
            return 8;
        }

        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_GENERATE = "totalGenerate";

        private const string CONFIG_KEY = "config_key";

        /*Config 相关*/
        private const string CONFIG_SDT_PRICE = "config_sdt_price";
        private const string CONFIG_SDT_RATE = "config_sdt_rate";



        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-06-06 15:16";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(SuperAdmin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //this is in nep5
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "decimals") return Decimals();
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }

                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    if (from == to)
                        return true;
                    if (from.Length == 0 || to.Length == 0)
                        return false;
                    BigInteger value = (BigInteger)args[2];
                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                        return false;
                    //如果有跳板调用，不让转
                    //if (ExecutionEngine.EntryScriptHash.AsBigInteger() != callscript.AsBigInteger())
                    //return false;

                    return Transfer(from, to, value);
                }

                if (operation == "setConfig")
                {

                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];

                    return SetConfig(key, value);
                }

                if (operation == "getCDP")
                {
                    if (args.Length != 1) return false;
                    byte[] onwer = (byte[])args[0];
                    return GetCDP(onwer);
                }

                if (operation == "openCDP")
                {
                    if (args.Length != 1) return false;
                    byte[] onwer = (byte[])args[0];

                    return OpenCDP(onwer);
                }

                if (operation == "lock")
                {
                    if (args.Length != 2) return false;
                    byte[] onwer = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    return Lock(onwer, value);
                }

                if (operation == "draw")
                {
                    if (args.Length != 2) return false;

                    byte[] onwer = (byte[])args[0];

                    BigInteger value = (BigInteger)args[0];

                    return Draw(onwer, value);
                }

            }

            return true;
        }

        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {

            if (value <= 0) return false;

            if (from == to) return true;

            //付款方
            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, from);
                else
                    Storage.Put(Storage.CurrentContext, from, from_value - value);
            }
            //收款方
            if (to.Length > 0)
            {
                BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
                Storage.Put(Storage.CurrentContext, to, to_value + value);
            }
            //记录交易信息
            // setTxInfo(from, to, value);
            //notify
            Transferred(from, to, value);
            return true;
        }

        public static Boolean SetConfig(string key, BigInteger value)
        {

            if (key == null || key == "") return false;

            if (!Runtime.CheckWitness(SuperAdmin)) return false;

            ConfigInfo configInfo = new ConfigInfo();

            if (key == "sdt_price")
            {
                configInfo.sdt_price = value;
            }

            if (key == "sdt_rate")
            {
                configInfo.sdt_rate = value;
            }

            byte[] config = Helper.Serialize(configInfo);

            Storage.Put(Storage.CurrentContext, CONFIG_KEY, config);

            return true;
        }

        public static ConfigInfo GetConfig()
        {

            string key = CONFIG_KEY;
            byte[] config = Storage.Get(Storage.CurrentContext, key);

            if (config.Length == 0) return null;

            ConfigInfo configInfo = (ConfigInfo)Helper.Deserialize(config);

            return configInfo;
        }

        /*查询债仓详情*/
        public static CDPTransferInfo GetCDP(byte[] onwer)
        {

            byte[] key = onwer.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);

            if (cdp.Length == 0) return null;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);

            return cdpInfo;
        }

        /*开启一个新的债仓*/
        public static Boolean OpenCDP(byte[] onwer)
        {

            if (!Runtime.CheckWitness(onwer)) return false;

            CDPTransferInfo cdpInfo_ = GetCDP(onwer);

            if (cdpInfo_ != null) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            CDPTransferInfo cdpInfo = new CDPTransferInfo();
            cdpInfo.onwer = onwer;
            cdpInfo.txid = txid;
            cdpInfo.locked = 0;
            cdpInfo.hasDrawed = 0;

            byte[] key = onwer.Concat(ConvertN(0));
            byte[] cdp = Helper.Serialize(cdpInfo);

            Storage.Put(Storage.CurrentContext, key, cdp);
            return true;
        }

        /*向债仓锁定数字资产*/
        public static Boolean Lock(byte[] onwer, BigInteger value)
        {
            if (value == 0) return false;

            if (!Runtime.CheckWitness(onwer)) return false;

            CDPTransferInfo cdpInfo = GetCDP(onwer);

            if (cdpInfo == null) return false;

            object[] arg = new object[3];
            arg[0] = onwer;
            arg[1] = SuperAdmin;
            arg[2] = value;

            if (!(bool)SDTContract("transfer", arg)) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            cdpInfo.locked = cdpInfo.locked + value;

            byte[] key = onwer.Concat(ConvertN(0));
            byte[] cdp = Helper.Serialize(cdpInfo);

            Storage.Put(Storage.CurrentContext, key, cdp);
            return true;
        }

        public static Boolean Draw(byte[] onwer, BigInteger drawSdusdValue)
        {

            if (drawSdusdValue == 0) return false;

            if (!Runtime.CheckWitness(onwer)) return false;

            CDPTransferInfo cdpInfo = GetCDP(onwer);

            if (cdpInfo == null) return false;

            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            ConfigInfo configInfo = GetConfig();

            BigInteger sdt_price = configInfo.sdt_price;
            BigInteger sdt_rate = configInfo.sdt_rate;

            BigInteger sdusd_limit = sdt_price * locked * 100 / sdt_rate;

            if (sdusd_limit < hasDrawed + drawSdusdValue) return false;

            Increase(onwer, drawSdusdValue);

            cdpInfo.hasDrawed = hasDrawed + drawSdusdValue;

            byte[] key = onwer.Concat(ConvertN(0));
            byte[] cdp = Helper.Serialize(cdpInfo);

            Storage.Put(Storage.CurrentContext, key, cdp);

            return true;
        }

        public class CDPTransferInfo
        {
            //拥有者
            public byte[] onwer;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如PNeo
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;
        }

        public class ConfigInfo
        {
            public BigInteger sdt_price;
            public BigInteger sdt_rate;


        }

        private static byte[] ConvertN(BigInteger n)
        {
            if (n == 0)
                return new byte[2] { 0x00, 0x00 };
            if (n == 1)
                return new byte[2] { 0x00, 0x01 };
            if (n == 2)
                return new byte[2] { 0x00, 0x02 };
            if (n == 3)
                return new byte[2] { 0x00, 0x03 };
            if (n == 4)
                return new byte[2] { 0x00, 0x04 };
            throw new Exception("not support.");
        }

        public static bool Increase(byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(to)) return false;

            Transfer(null, to, value);

            operateTotalSupply(value);
            return true;
        }

        public static bool operateTotalSupply(BigInteger mount)
        {
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + mount >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current + mount);
            }
            return true;
        }

    }
}
