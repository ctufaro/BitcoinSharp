/*
 * Copyright 2011 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BitCoinSharp.IO;
using log4net;

namespace BitCoinSharp
{
    /// <summary>
    /// BitCoin transactions don't specify what they do directly. Instead <a href="https://en.bitcoin.it/wiki/Script">a small binary stack language</a>
    /// is used to define programs that when evaluated return whether the transaction
    /// "accepts" or rejects the other transactions connected to it.
    /// </summary>
    /// <remarks>
    /// This implementation of the scripting language is incomplete. It contains enough support to run standard
    /// transactions generated by the official client, but non-standard transactions will fail.
    /// </remarks>
    public class Script
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (Script));

        // Some constants used for decoding the scripts.
        public const int OpPushData1 = 76;
        public const int OpPushData2 = 77;
        public const int OpPushData4 = 78;
        public const int OpDup = 118;
        public const int OpHash160 = 169;
        public const int OpEqualVerify = 136;
        public const int OpCheckSig = 172;

        private byte[] _program;
        private int _cursor;

        // The program is a set of byte[]s where each element is either [opcode] or [data, data, data ...]
        private IList<byte[]> _chunks;
        private byte[] _programCopy; // TODO: remove this
        private readonly NetworkParameters _params;

        /// <summary>
        /// Construct a Script using the given network parameters and a range of the programBytes array.
        /// </summary>
        /// <param name="params">Network parameters.</param>
        /// <param name="programBytes">Array of program bytes from a transaction.</param>
        /// <param name="offset">How many bytes into programBytes to start reading from.</param>
        /// <param name="length">How many bytes to read.</param>
        /// <exception cref="ScriptException"/>
        public Script(NetworkParameters @params, byte[] programBytes, int offset, int length)
        {
            _params = @params;
            Parse(programBytes, offset, length);
        }

        /// <summary>
        /// Returns the program opcodes as a string, for example "[1234] DUP HAHS160"
        /// </summary>
        public override string ToString()
        {
            var buf = new StringBuilder();
            foreach (var chunk in _chunks)
            {
                if (chunk.Length == 1)
                {
                    string opName;
                    var opcode = chunk[0];
                    switch (opcode)
                    {
                        case OpDup:
                            opName = "DUP";
                            break;
                        case OpHash160:
                            opName = "HASH160";
                            break;
                        case OpCheckSig:
                            opName = "CHECKSIG";
                            break;
                        case OpEqualVerify:
                            opName = "EQUALVERIFY";
                            break;
                        default:
                            opName = "?(" + opcode + ")";
                            break;
                    }
                    buf.Append(opName);
                    buf.Append(" ");
                }
                else
                {
                    // Data chunk
                    buf.Append("[");
                    buf.Append(chunk.Length);
                    buf.Append("]");
                    buf.Append(Utils.BytesToHexString(chunk));
                    buf.Append(" ");
                }
            }
            return buf.ToString();
        }

        /// <exception cref="ScriptException"/>
        private byte[] GetData(int len)
        {
            try
            {
                var buf = new byte[len];
                Array.Copy(_program, _cursor, buf, 0, len);
                _cursor += len;
                return buf;
            }
            catch (IndexOutOfRangeException e)
            {
                // We want running out of data in the array to be treated as a recoverable script parsing exception,
                // not something that abnormally terminates the app.
                throw new ScriptException("Failed read of " + len + " bytes", e);
            }
        }

        private byte ReadByte()
        {
            return _program[_cursor++];
        }

        /// <summary>
        /// To run a script, first we parse it which breaks it up into chunks representing pushes of
        /// data or logical opcodes. Then we can run the parsed chunks.
        /// </summary>
        /// <remarks>
        /// The reason for this split, instead of just interpreting directly, is to make it easier
        /// to reach into a programs structure and pull out bits of data without having to run it.
        /// This is necessary to render the to/from addresses of transactions in a user interface.
        /// The official client does something similar.
        /// </remarks>
        /// <exception cref="ScriptException"/>
        private void Parse(byte[] programBytes, int offset, int length)
        {
            // TODO: this is inefficient
            _programCopy = new byte[length];
            Array.Copy(programBytes, offset, _programCopy, 0, length);

            _program = _programCopy;
            offset = 0;
            _chunks = new List<byte[]>(10); // Arbitrary choice of initial size.
            _cursor = offset;
            while (_cursor < offset + length)
            {
                var opcode = (int) ReadByte();
                if (opcode >= 0xF0)
                {
                    // Not a single byte opcode.
                    opcode = (opcode << 8) | ReadByte();
                }

                if (opcode > 0 && opcode < OpPushData1)
                {
                    // Read some bytes of data, where how many is the opcode value itself.
                    _chunks.Add(GetData(opcode)); // opcode == len here.
                }
                else if (opcode == OpPushData1)
                {
                    var len = ReadByte();
                    _chunks.Add(GetData(len));
                }
                else if (opcode == OpPushData2)
                {
                    // Read a short, then read that many bytes of data.
                    var len = ReadByte() | (ReadByte() << 8);
                    _chunks.Add(GetData(len));
                }
                else if (opcode == OpPushData4)
                {
                    // Read a uint32, then read that many bytes of data.
                    _log.Error("PUSHDATA4: Unimplemented");
                }
                else
                {
                    _chunks.Add(new[] {(byte) opcode});
                }
            }
        }

        /// <summary>
        /// Returns true if this transaction is of a format that means it was a direct IP to IP transaction. These
        /// transactions are deprecated and no longer used, support for creating them has been removed from the official
        /// client.
        /// </summary>
        public bool IsSentToIp
        {
            get { return _chunks.Count == 2 && (_chunks[1][0] == OpCheckSig && _chunks[0].Length > 1); }
        }

        /// <summary>
        /// If a program matches the standard template DUP HASH160 &lt;pubkey hash&gt; EQUALVERIFY CHECKSIG
        /// then this function retrieves the third element, otherwise it throws a ScriptException.
        /// </summary>
        /// <remarks>
        /// This is useful for fetching the destination address of a transaction.
        /// </remarks>
        /// <exception cref="ScriptException"/>
        public byte[] PubKeyHash
        {
            get
            {
                if (_chunks.Count != 5)
                    throw new ScriptException("Script not of right size to be a scriptPubKey, " +
                                              "expecting 5 but got " + _chunks.Count);
                if (_chunks[0][0] != OpDup ||
                    _chunks[1][0] != OpHash160 ||
                    _chunks[3][0] != OpEqualVerify ||
                    _chunks[4][0] != OpCheckSig)
                    throw new ScriptException("Script not in the standard scriptPubKey form");

                // Otherwise, the third element is the hash of the public key, ie the BitCoin address.
                return _chunks[2];
            }
        }

        /// <summary>
        /// If a program has two data buffers (constants) and nothing else, the second one is returned.
        /// For a scriptSig this should be the public key of the sender.
        /// </summary>
        /// <remarks>
        /// This is useful for fetching the source address of a transaction.
        /// </remarks>
        /// <exception cref="ScriptException"/>
        public byte[] PubKey
        {
            get
            {
                if (_chunks.Count == 1)
                {
                    // Direct IP to IP transactions only have the public key in their scriptSig.
                    return _chunks[0];
                }
                if (_chunks.Count != 2)
                    throw new ScriptException("Script not of right size to be a scriptSig, expecting 2" +
                                              " but got " + _chunks.Count);
                if (!(_chunks[0].Length > 1) && (_chunks[1].Length > 1))
                    throw new ScriptException("Script not in the standard scriptSig form: " +
                                              _chunks.Count + " chunks");
                return _chunks[1];
            }
        }

        /// <summary>
        /// Convenience wrapper around getPubKey. Only works for scriptSigs.
        /// </summary>
        /// <exception cref="ScriptException"/>
        public Address FromAddress
        {
            get { return new Address(_params, Utils.Sha256Hash160(PubKey)); }
        }

        /// <summary>
        /// Gets the destination address from this script, if it's in the required form (see getPubKey).
        /// </summary>
        /// <exception cref="ScriptException"/>
        public Address ToAddress
        {
            get { return new Address(_params, PubKeyHash); }
        }

        ////////////////////// Interface for writing scripts from scratch ////////////////////////////////

        /// <summary>
        /// Writes out the given byte buffer to the output stream with the correct opcode prefix
        /// </summary>
        /// <exception cref="IOException"/>
        public static void WriteBytes(Stream os, byte[] buf)
        {
            if (buf.Length < OpPushData1)
            {
                os.Write((byte) buf.Length);
                os.Write(buf);
            }
            else if (buf.Length < 256)
            {
                os.Write(OpPushData1);
                os.Write((byte) buf.Length);
                os.Write(buf);
            }
            else if (buf.Length < 65536)
            {
                os.Write(OpPushData2);
                os.Write((byte) buf.Length);
                os.Write((byte) (buf.Length >> 8));
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static byte[] CreateOutputScript(Address to)
        {
            using (var bits = new MemoryStream())
            {
                // TODO: Do this by creating a Script *first* then having the script reassemble itself into bytes.
                bits.Write(OpDup);
                bits.Write(OpHash160);
                WriteBytes(bits, to.Hash160);
                bits.Write(OpEqualVerify);
                bits.Write(OpCheckSig);
                return bits.ToArray();
            }
        }

        /// <summary>
        /// Create a script that sends coins directly to the given public key (eg in a coinbase transaction).
        /// </summary>
        public static byte[] CreateOutputScript(byte[] pubkey)
        {
            // TODO: Do this by creating a Script *first* then having the script reassemble itself into bytes.
            using (var bits = new MemoryStream())
            {
                WriteBytes(bits, pubkey);
                bits.Write(OpCheckSig);
                return bits.ToArray();
            }
        }

        public static byte[] CreateInputScript(byte[] signature, byte[] pubkey)
        {
            // TODO: Do this by creating a Script *first* then having the script reassemble itself into bytes.
            using (var bits = new MemoryStream())
            {
                WriteBytes(bits, signature);
                WriteBytes(bits, pubkey);
                return bits.ToArray();
            }
        }
    }
}