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
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        [Appcall("b0a90b73c6804cbb2d94e4802e084c076bc099e6")] 
        public static extern object SDTContract(string method, object[] args);

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

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

        private const string STORAGE_ACCOUNT = "storage_account"; 
        private const string STORAGE_TXID = "storage_txid";

        //交易类型
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_LOCK = 1,//锁仓
            TRANSACTION_TYPE_DRAW,//提取
            TRANSACTION_TYPE_FREE,//释放
            TRANSACTION_TYPE_WIPE,//赎回
            TRANSACTION_TYPE_SHUT,//关闭
            TRANSACTION_TYPE_FORCESHUT,//对手关闭
            TRANSACTION_TYPE_GIVE,//转移所有权
        }


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-06-20 15:16";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(SuperAdmin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //this is in nep5
                if (operation == "totalSupply") return totalSupply();
                if (operation == "name") return name();
                if (operation == "symbol") return symbol();
                if (operation == "decimals") return decimals();
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return balanceOf(account);
                }
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                        return false;

                    return transfer(from, to, value);
                }

                if (operation == "setAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];
                    if (!Runtime.CheckWitness(SuperAdmin)) return false;
                    return setAccount(address);
                }
                 
                if (operation == "setConfig")
                { 
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];
                    return setConfig(key, value); 
                }

                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];
                    return getConfig(key);
                }

                if (operation == "getCDP")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return getCDP(addr);
                }

                if (operation == "openCDP")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return openCDP(addr);
                }

                if (operation == "lock")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return Lock(addr, value);
                }

                if (operation == "draw")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[0];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return draw(addr, value);
                }
                //管理员操作
                if (operation == "shut")
                {
                    if (args.Length != 1) return false;
                    //用户地址
                    byte[] addr = (byte[])args[0];
                    if (!Runtime.CheckWitness(SuperAdmin)) return false;

                    return shut(addr);

                }
                //测试合约转账
                if (operation == "transfer_sdt")
                {
                    if (args.Length != 2) return false;
                    byte[] to = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(SuperAdmin)) return false;

                    //本合约地址
                    byte[] addr = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
                    object[] arg = new object[3];
                    arg[0] = addr;
                    arg[1] = to;
                    arg[2] = value;
                    if (!(bool)SDTContract("transfer_contract", arg)) return false;
                    return true;
                }

                //合约调用授权转账
                if (operation == "test_transferfrom")
                {
                    if (args.Length != 3) return false;
                    //本合约地址
                    byte[] addr = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);

                    object[] arg = new object[4];
                    arg[0] = args[0];
                    arg[1] = addr;
                    arg[2] = args[1];
                    arg[3] = args[2];
                    if (!(bool)SDTContract("transferFrom", arg)) return false;
                    return true;
                }
            }
            return true;
        }


        //nep5 func
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string name()
        {
            return "AA USD";
        }
        public static string symbol()
        {
            return "AAUSD";
        }

        public static byte decimals()
        {
            return 8;
        }

        public static BigInteger balanceOf(byte[] address)
        {
            if (address.Length != 20) return 0;
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        private static Boolean shut(byte[] addr)
        {
            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);

            byte[] owner = cdpInfo.owner;
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //当前余额必须要大于负债
            BigInteger balance = balanceOf(addr);
            if (hasDrawed > balance) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //从合约地址转账
            byte[] from = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            object[] arg = new object[3];
            arg[0] = from;
            arg[1] = owner;
            arg[2] = locked;

            if (!(bool)SDTContract("transfer_contract", arg)) return false;

            if (hasDrawed > 0)
            {
                //先要销毁SD
                transfer(addr, null, hasDrawed);
                //减去总量
                operateTotalSupply(0 - hasDrawed);
            }
            //关闭CDP
            Storage.Delete(Storage.CurrentContext, key);

            //记录交易详细数据
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        public static bool transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (from.Length != 20 || to.Length != 20) return false;

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
            //setTxInfo(from, to, value);
            //notify
            Transferred(from, to, value);
            return true;
        }

        public static bool setAccount(byte[] address)
        { 
            byte[] addr = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);

            if (addr.Length != 0) return false;

            Storage.Put(Storage.CurrentContext, STORAGE_ACCOUNT, address);
            return true;
        }

        public static bool setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            //只允许超管操作
            if (!Runtime.CheckWitness(SuperAdmin)) return false;

            Storage.Put(Storage.CurrentContext, key.AsByteArray(), value);

            return true; 
        }

        public static BigInteger getConfig(string key)
        {
            if (key == null || key == "") return 0;

            return Storage.Get(Storage.CurrentContext, key.AsByteArray()).AsBigInteger();
        }

        /*查询债仓详情*/
        public static CDPTransferInfo getCDP(byte[] onwer)
        {

            byte[] key = onwer.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);

            if (cdp.Length == 0) return null;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);

            return cdpInfo;
        }

        /*开启一个新的债仓*/
        public static bool openCDP(byte[] addr)
        {
            if (addr.Length != 20) return false;

            CDPTransferInfo cdpInfo_ = getCDP(addr);

            if (cdpInfo_ != null) return false;

            byte[] txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            CDPTransferInfo cdpInfo = new CDPTransferInfo();
            cdpInfo.owner = addr;
            cdpInfo.txid = txid;
            cdpInfo.locked = 0;
            cdpInfo.hasDrawed = 0;
             
            byte[] key = addr.Concat(ConvertN(0));
            byte[] cdp = Helper.Serialize(cdpInfo);

            Storage.Put(Storage.CurrentContext, key, cdp);
            return true;
        }

        /*向债仓锁定数字资产*/
        public static bool Lock(byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;

            if (value == 0) return false;

            if (!Runtime.CheckWitness(addr)) return false;

            byte[] key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = value;

            if (!(bool)SDTContract("transfer", arg)) return false;
            
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] obj = new object[1];
            obj[0] = txid;

            TransferInfo transferInfo = (TransferInfo)SDTContract("getTXInfo", obj);


            /*校验交易信息*/
            if (transferInfo.from != addr || transferInfo.to != to || value != transferInfo.value) return false;

            byte[] used = Storage.Get(Storage.CurrentContext, txid);
            /*判断txid是否已被使用*/
            if (used.Length != 0) return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);

            cdpInfo.locked = cdpInfo.locked + value; 
            BigInteger currLock = cdpInfo.locked;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));

            //记录交易详细数据
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_LOCK;
            detail.operated = value;
            detail.hasLocked = currLock;
            detail.hasDrawed = cdpInfo.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));

            /*记录txid 已被使用*/
            Storage.Put(Storage.CurrentContext, txid, addr);
            return true;
        }

        public static bool draw(byte[] addr, BigInteger drawSdusdValue)
        {
            if (addr.Length != 20) return false;

            if (drawSdusdValue <= 0) return false;

            byte[] key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0) return false;
            //获取CDP
            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;
             
            BigInteger sdt_price = getConfig(CONFIG_SDT_PRICE); 
            BigInteger sdt_rate = getConfig(CONFIG_SDT_RATE); ;

            BigInteger sdusd_limit = sdt_price * locked * 100 / sdt_rate;

            if (sdusd_limit < hasDrawed + drawSdusdValue) return false;

            Increase(addr, drawSdusdValue);

            cdpInfo.hasDrawed = hasDrawed + drawSdusdValue;


            Storage.Put(Storage.CurrentContext, key, cdp);
            
            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DRAW;
            detail.operated = drawSdusdValue;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;

            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
             
            return true;
        }

        public class CDPTransferInfo
        {
            //拥有者
            public byte[] owner;

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

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }


        public class CDPTransferDetail
        {
            //地址
            public byte[] from;

            //CDP交易序号
            public byte[] cdpTxid;

            //交易序号
            public byte[] txid;

            //操作对应资产的金额,如PNeo
            public BigInteger operated;

            //已经被锁定的资产金额,如PNeo
            public BigInteger hasLocked;

            //已经提取的资产金额，如SDUSDT  
            public BigInteger hasDrawed;

            //操作类型
            public int type;
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

            transfer(null, to, value);

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
