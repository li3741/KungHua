using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KungHua.BT.BenCode;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace KungHua.DHT
{
    public class DHTCrawler
    {
        private DHTCrawler()
        {
            boostrapAddr = new IPEndPoint(IPAddress.Parse("67.215.246.10"), 6881);
            //初始化集合
            lt_locks = new List<string>();
            lt_Nodes = new List<HashSet<Tuple<string, IPEndPoint>>>();
            for (var i = 0; i < DHT_BUCKETS; i++)
            {
                lt_locks.Add("x");
                lt_Nodes.Add(new HashSet<Tuple<string, IPEndPoint>>());
            }
            //设置获取随机数
            random = new Random();
            nodeIDHex = getRandomID();
            conversationID = getRandomConvID();
            //私有化构造函数
            _ReceviceUDP = new UdpClient();//用来接收所有进来的UDP
            _ReceviceUDP.Client.ReceiveBufferSize = 10000000; // in bytes
            _ReceviceUDP.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            _ReceviceUDP.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _ReceviceUDP.Client.Ttl = 255;
            _ReceviceUDP.Client.Bind(new IPEndPoint(IPAddress.Any, localPort));

            _SendUDP = new UdpClient();//发送UDP使用的
            _SendUDP.Client.SendBufferSize = 10000000;
            _SendUDP.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _SendUDP.Client.Ttl = 255;
            _SendUDP.Client.Bind(new IPEndPoint(IPAddress.Any, localPort));

            //开始一个线程进行监听消息
            _ReceiveThread = new System.Threading.Thread(new System.Threading.ThreadStart(ReceviceMessage));
            _ReceiveThread.IsBackground = true;
            _ReceiveThread.Start();

        }

        public static DHTCrawler Create()
        {
            var inist = new DHTCrawler();
            return inist;
        }

        public void Run()
        {
            //开始运行
            //从一个已经知道的tracker开始重复获取getpeers,如果得到nodes，则保存在联系人列表里面，如果得到hashinfo，则保存到种子库里面
            //遍历联系人列表，重复获取getpeers,如果得到nodes则保存在联系人列表里面，如果得到hashinfo，则保存到种子库里面
            while (has_node_count < MAX_NODES_COUNT)
            {
                GetPeers(boostrapAddr, getRandomID());
            }

            while (has_peer_count < MAX_PEERS_COUNT)
            {
                for (var i = 0; i < DHT_BUCKETS && has_peer_count < MAX_PEERS_COUNT; i++)
                {
                    lock (lt_locks[i])
                    {
                        foreach (var item in lt_Nodes[i])
                        {
                            GetBuckets(item.Item2);
                        }
                    }
                }
            }

            //完成遍历，中断接收
            _ReceiveThread.Abort();
            //退出运行
        }

        #region 私有属性
        private UdpClient _ReceviceUDP;
        private UdpClient _SendUDP;
        private System.Threading.Thread _ReceiveThread;


        private int SIO_UDP_CONNRESET = -1744830452;
        private int localPort = 6881;//设置端口
        private int DHT_BUCKETS = 65536;//为什么桶数是这个？

        private int MAX_PEERS_COUNT = 100;//这个计算对等点的，获取每个对等点的桶数吗？
        private int MAX_NODES_COUNT = 1000000;//节点上限

        private List<HashSet<Tuple<string, IPEndPoint>>> lt_Nodes;//节点列表
        private List<string> lt_locks;

        private string nodeIDHex;//自己的节点ID
        private string conversationID;//这个id是一直用？
        private int has_node_count = 0;//当前节点总数
        private int has_peer_count = 0;//当前peer点总数

        private Random random;//全局通用随机数

        private IPEndPoint boostrapAddr;
        #endregion

        #region 私有方法
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="sendTo"></param>
        /// <param name="message"></param>
        void SendMessage(IPEndPoint sendTo, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Log("不能发送空包！");
                return;
            }
            byte[] send_buffer = BencodingUtils.ExtendedASCIIEncoding.GetBytes(message);
            try
            {
                _SendUDP.Send(send_buffer, send_buffer.Length, sendTo);
                Log(message);
                send_buffer = null;
            }
            catch (Exception ex)
            {
                Log("发送消息异常：" + ex.ToString());
            }
        }
        /// <summary>
        /// 接收消息
        /// </summary>
        void ReceviceMessage()
        {
            IPEndPoint receiveFrom;
            while (true)
            {
                try
                {
                    receiveFrom = new IPEndPoint(IPAddress.Any, localPort);//设置监听端口
                    byte[] data = _ReceviceUDP.Receive(ref receiveFrom);//接收消息
                    Stream stream = new MemoryStream(data);//获取数据流
                    IBencodingType receivedMsg = BencodingUtils.Decode(stream);//进行解码
                    string decoded = BencodingUtils.ExtendedASCIIEncoding.GetString(data.ToArray());
                    Log("收到消息" + DateTime.Now.ToString());
                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        Log(decoded);
                    }
                    // t is transaction id
                    // y is e for error, r for reply, q for query
                    if (receivedMsg is BDict)//如果接收到其他的数据，忽略
                    {
                        BDict dictMsg = (BDict)receivedMsg;
                        if (dictMsg.ContainsKey("y"))
                        {
                            if (dictMsg["y"].Equals(new BString("e")))
                            {
                                Log("接收到错误信息提示! (已经忽略)");
                            }
                            else if (dictMsg["y"].Equals(new BString("r")))
                            {
                                // received reply
                                if (dictMsg.ContainsKey("r"))
                                {
                                    if (dictMsg["r"] is BDict)
                                    {
                                        BDict dictMsg2 = (BDict)dictMsg["r"];
                                        if (dictMsg2.ContainsKey("values"))
                                        {
                                            Log("接收到种子节点列表，解析后入库!");
                                            BString peers = (BString)dictMsg2["values"];
                                            UpdateBTHashList(peers);
                                        }
                                        else if (dictMsg2.ContainsKey("nodes"))
                                        {
                                            //也可能是询问的节点 could be an answer to find node or get peers
                                            Log("接收到节点（NodeId,IP,Port）!");
                                            BString nodeIDString = (BString)dictMsg2["nodes"];
                                            UpdateContactList(nodeIDString);//更新到联系人列表中
                                        }
                                        else
                                        {
                                            // no values and no nodes, assuming its a ping,
                                            // at least some form of response
                                        }
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                            else if (dictMsg["y"].Equals(new BString("q")))
                            {
                                // received query
                                Log("接收到询问信息！（忽略）");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("接收数据函数异常：" + ex.ToString());
                }
            }

        }

        private void UpdateBTHashList(BString peers)
        {
            byte[] arr = BencodingUtils.ExtendedASCIIEncoding.GetBytes(peers.Value);
        }
        /// <summary>
        /// 更新节点信息
        /// </summary>
        /// <param name="nodeIDString"></param>
        private void UpdateContactList(BString nodeIDString)
        {
            byte[] arr = BencodingUtils.ExtendedASCIIEncoding.GetBytes(nodeIDString.Value);
            byte[] ip = new byte[4];
            byte[] port = new byte[2];
            byte[] nodeID = new byte[20];
            byte[] currLock;
            int bucketNr;
            for (int i = 0; i < arr.Length / 26; i++)
            {
                currLock = new byte[4] { 0, 0, 0, 0 };
                Array.Copy(arr, i * 26, nodeID, 0, 20);//20位存储nodeid
                Array.Copy(arr, i * 26, currLock, 2, 2);//用nodeid的第三位起，取两位，用来按远近放置节点？，
                Array.Copy(arr, i * 26 + 20, ip, 0, 4);//IP
                Array.Copy(arr, i * 26 + 24, port, 0, 2);//端口
                Array.Reverse(currLock);
                bucketNr = BitConverter.ToInt32(currLock, 0);
                Array.Reverse(port);
                IPEndPoint ipEndP = new IPEndPoint((Int64)BitConverter.ToUInt32(ip, 0), (Int32)BitConverter.ToUInt16(port, 0));
                string sNodeID = ByteArrayToHexString(nodeID);

                if ((Int64)BitConverter.ToUInt32(ip, 0) != 0 && (Int32)BitConverter.ToUInt16(port, 0) != 0)
                {
                    lock (lt_locks[bucketNr])//用字符串来锁住对应的节点来判断或者添加
                    {
                        if (!lt_Nodes[bucketNr].Contains(Tuple.Create(sNodeID, ipEndP)))
                        {
                            lt_Nodes[bucketNr].Add(Tuple.Create(sNodeID, ipEndP));
                            has_node_count++;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="p"></param>
        private void Log(string p)
        {
            Console.WriteLine(p);
            System.Diagnostics.Debug.WriteLine(p);
        }

        private void GetPeers(IPEndPoint sendTo, string infohash)
        {
            string message = string.Concat("d1:ad2:id20:", HexSToString(nodeIDHex), "9:info_hash20:", HexSToString(infohash), "e1:q9:get_peers1:t2:", HexSToString(conversationID), "1:v4:abcd1:y1:qe");//这里直接拼成品，不用转换
            SendMessage(sendTo, message);
        }
        private void GetBuckets(IPEndPoint sendTo)
        {
            for (var i = 0; i < MAX_PEERS_COUNT; i++)
            {
                string infohash = getRandomID();
                GetPeers(sendTo, infohash);
            }
        }

        #endregion



        #region 转换方法
        private string ByteArrayToHexString(byte[] arr)
        {
            string hex = BitConverter.ToString(arr);
            return hex.Replace("-", "");
        }
        private string HexSToString(string hexString)
        {
            int length = hexString.Length / 2;
            byte[] arr = new byte[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = Convert.ToByte(hexString.Substring(2 * i, 2), 16);
            }
            return BencodingUtils.ExtendedASCIIEncoding.GetString(arr);
        }
        private string getRandomID()
        {
            byte[] b = new byte[20];
            random.NextBytes(b);
            return ByteArrayToHexString(b);
        }
        private string getRandomConvID()
        {
            byte[] b = new byte[2];
            random.NextBytes(b);
            return ByteArrayToHexString(b);
        }
        IEnumerable<bool> GetBits(byte b)
        {
            for (int i = 0; i < 8; i++)
            {
                yield return (b & 0x80) != 0;
                b *= 2;
            }
        }
        #endregion

    }
}
