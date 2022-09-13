using UnityEngine;
using System.Collections;
using System;
using System.IO;

namespace Smooth
{
    /// <summary>
    /// The state of an object: timestamp, position, rotation, scale, velocity, angular velocity.
    /// </summary>
    public class StatePUN
    {
        /// <summary>
        /// The network timestamp of the owner when the state was sent.
        /// </summary>
        public float ownerTimestamp;
        /// <summary>
        /// The position of the owned object when the state was sent.
        /// </summary>
        public Vector3 position;
        /// <summary>
        /// The rotation of the owned object when the state was sent.
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// The scale of the owned object when the state was sent.
        /// </summary>
        public Vector3 scale;
        /// <summary>
        /// The velocity of the owned object when the state was sent.
        /// </summary>
        public Vector3 velocity;
        /// <summary>
        /// The angularVelocity of the owned object when the state was sent.
        /// </summary>
        public Vector3 angularVelocity;
        /// <summary>
        /// If this State is tagged as a teleport State, it should be moved immediately to instead of lerped to.
        /// </summary>
        public bool teleport;
        /// <summary>
        /// If this State is tagged as a positional rest State, it should stop extrapolating position on non-owners.
        /// </summary>
        public bool atPositionalRest;
        /// <summary>
        /// If this State is tagged as a rotational rest State, it should stop extrapolating rotation on non-owners.
        /// </summary>
        public bool atRotationalRest;

        /// <summary>
        /// The time on the server when the State is validated. Only used by server for latestVerifiedState.
        /// </summary>
        public float receivedOnServerTimestamp;

        /// <summary>
        /// Used in Deserialize() so we don't have to make a new Vector3 every time.
        /// </summary>
        public Vector3 reusableRotationVector;

        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayPosition = false;
        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayRotation = false;
        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayScale = false;
        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayVelocity = false;
        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayAngularVelocity = false;
        /// <summary>
        /// The server will set this to true if it is received so we know to relay the information back out to other clients.
        /// </summary>
        public bool serverShouldRelayTeleport = false;
        
        /// <summary>The localTime that a state was received on a non-owner.</summary>
        public float receivedTimestamp;

        /// <summary>This value is incremented each time local time is reset so that non-owners can detect and handle the reset.</summary>
        public int localTimeResetIndicator;

        /// <summary>
        /// Default constructor. Does nothing.
        /// </summary>
        public StatePUN() { }

        /// <summary>
        /// Copy an existing State.
        /// </summary>
        public StatePUN copyFromState(StatePUN state)
        {
            ownerTimestamp = state.ownerTimestamp;
            position = state.position;
            rotation = state.rotation;
            scale = state.scale;
            velocity = state.velocity;
            angularVelocity = state.angularVelocity;
            receivedTimestamp = state.receivedTimestamp;
            localTimeResetIndicator = state.localTimeResetIndicator;
            return this;
        }

        /// <summary>
        /// Returns a Lerped state that is between two States in time.
        /// </summary>
        /// <param name="start">Start State</param>
        /// <param name="end">End State</param>
        /// <param name="t">Time</param>
        /// <returns></returns>
        public static StatePUN Lerp(StatePUN targetTempState, StatePUN start, StatePUN end, float t)
        {
            targetTempState.position = Vector3.Lerp(start.position, end.position, t);
            targetTempState.rotation = Quaternion.Lerp(start.rotation, end.rotation, t);
            targetTempState.scale = Vector3.Lerp(start.scale, end.scale, t);
            targetTempState.velocity = Vector3.Lerp(start.velocity, end.velocity, t);
            targetTempState.angularVelocity = Vector3.Lerp(start.angularVelocity, end.angularVelocity, t);

            targetTempState.ownerTimestamp = Mathf.Lerp(start.ownerTimestamp, end.ownerTimestamp, t);

            return targetTempState;
        }

        public void resetTheVariables()
        {
            ownerTimestamp = 0;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.zero;
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            atPositionalRest = false;
            atRotationalRest = false;
            teleport = false;
            receivedTimestamp = 0;
            localTimeResetIndicator = 0;
        }

        public void copyFromSmoothSync(SmoothSyncPUN smoothSyncScript)
        {
            ownerTimestamp = smoothSyncScript.localTime;
            position = smoothSyncScript.getPosition();
            rotation = smoothSyncScript.getRotation();
            scale = smoothSyncScript.getScale();

            if (smoothSyncScript.hasRigidbody)
            {
                velocity = smoothSyncScript.rb.velocity;
                angularVelocity = smoothSyncScript.rb.angularVelocity * Mathf.Rad2Deg;
            }
            else if (smoothSyncScript.hasRigidbody2D)
            {
                velocity = smoothSyncScript.rb2D.velocity;
                angularVelocity.x = 0;
                angularVelocity.y = 0;
                angularVelocity.z = smoothSyncScript.rb2D.angularVelocity;
            }
            else
            {
                velocity = Vector3.zero;
                angularVelocity = Vector3.zero;
            }
            localTimeResetIndicator = smoothSyncScript.localTimeResetIndicator;
            //atPositionalRest = smoothSyncScript.sendAtPositionalRestMessage;
            //atRotationalRest = smoothSyncScript.sendAtRotationalRestMessage;
        }
    }

    /// <summary>
    /// Wraps the State in the NetworkMessage so we can send it over the network.
    /// </summary>
    /// <remarks>
    /// This only sends and receives the parts of the State that are enabled on the SmoothSync component.
    /// </remarks>
    public class NetworkStatePUN
    {
        /// <summary>
        /// The SmoothSync object associated with this State.
        /// </summary>
        public SmoothSyncPUN smoothSync;
        /// <summary>
        /// The State that will be sent over the network
        /// </summary>
        public StatePUN state = new StatePUN();

        /// <summary>
        /// Default contstructor, does nothing.
        /// </summary>
        public NetworkStatePUN() { }

        public NetworkStatePUN(SmoothSyncPUN smoothSyncScript)
        {
            this.smoothSync = smoothSyncScript;
            state.copyFromSmoothSync(smoothSyncScript);
        }
        /// <summary>
        /// Copy the SmoothSync object to a NetworkState.
        /// </summary>
        /// <param name="smoothSyncScript">The SmoothSync object</param>
        public void copyFromSmoothSync(SmoothSyncPUN smoothSyncScript)
        {
            this.smoothSync = smoothSyncScript;
            state.copyFromSmoothSync(smoothSyncScript);
        }
        /// <summary>
        /// Serialize the message over the network.
        /// </summary>
        /// <remarks>
        /// Only sends what it needs and compresses floats if you chose to.
        /// </remarks>
        /// <param name="writer">The NetworkWriter to write to.</param>
        public void Serialize(BinaryWriter writer)
        {
            bool sendPosition, sendRotation, sendScale, sendVelocity, sendAngularVelocity, sendAtPositionalRestTag, sendAtRotationalRestTag;

            //// If is a server trying to relay client information back out to other clients.
            //if (NetworkServer.active && !smoothSync.photonView.IsMine)//!smoothSync.hasAuthority)
            //{
            //    sendPosition = state.serverShouldRelayPosition;
            //    sendRotation = state.serverShouldRelayRotation;
            //    sendScale = state.serverShouldRelayScale;
            //    sendVelocity = state.serverShouldRelayVelocity;
            //    sendAngularVelocity = state.serverShouldRelayAngularVelocity;
            //    sendTeleportTag = state.teleport;
            //    sendAtPositionalRestTag = state.atPositionalRest;
            //    sendAtRotationalRestTag = state.atRotationalRest;
            //}
            //else // If is a server or client trying to send owned object information across the network.
            {
                sendPosition = smoothSync.sendPosition;
                sendRotation = smoothSync.sendRotation;
                sendScale = smoothSync.sendScale;
                sendVelocity = smoothSync.sendVelocity;
                sendAngularVelocity = smoothSync.sendAngularVelocity;
                sendAtPositionalRestTag = smoothSync.sendAtPositionalRestMessage;
                sendAtRotationalRestTag = smoothSync.sendAtRotationalRestMessage;
            }
            //// Only set last sync States on clients here because the server needs to send multiple Serializes.
            //if (!NetworkServer.active)
            {
                if (sendPosition) smoothSync.lastPositionWhenStateWasSent = state.position;
                if (sendRotation) smoothSync.lastRotationWhenStateWasSent = state.rotation;
                if (sendScale) smoothSync.lastScaleWhenStateWasSent = state.scale;
                if (sendVelocity) smoothSync.lastVelocityWhenStateWasSent = state.velocity;
                if (sendAngularVelocity) smoothSync.lastAngularVelocityWhenStateWasSent = state.angularVelocity;
            }

            writer.Write(encodeSyncInformation(sendPosition, sendRotation, sendScale,
                sendVelocity, sendAngularVelocity, sendAtPositionalRestTag, sendAtRotationalRestTag));
            //writer.Write(smoothSync.netID);
            writer.Write((uint)smoothSync.syncIndex);
            writer.Write(state.ownerTimestamp);

            // Write position.
            if (sendPosition)
            {
                if (smoothSync.isPositionCompressed)
                {
                    if (smoothSync.isSyncingXPosition)
                    {
                        writer.Write(HalfHelper.Compress(state.position.x));
                    }
                    if (smoothSync.isSyncingYPosition)
                    {
                        writer.Write(HalfHelper.Compress(state.position.y));
                    }
                    if (smoothSync.isSyncingZPosition)
                    {
                        writer.Write(HalfHelper.Compress(state.position.z));
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXPosition)
                    {
                        writer.Write(state.position.x);
                    }
                    if (smoothSync.isSyncingYPosition)
                    {
                        writer.Write(state.position.y);
                    }
                    if (smoothSync.isSyncingZPosition)
                    {
                        writer.Write(state.position.z);
                    }
                }
            }
            // Write rotation.
            if (sendRotation)
            {
                Vector3 rot = state.rotation.eulerAngles;
                if (smoothSync.isRotationCompressed)
                {
                    // Convert to radians for more accurate Half numbers
                    if (smoothSync.isSyncingXRotation)
                    {
                        writer.Write(HalfHelper.Compress(rot.x * Mathf.Deg2Rad));
                    }
                    if (smoothSync.isSyncingYRotation)
                    {
                        writer.Write(HalfHelper.Compress(rot.y * Mathf.Deg2Rad));
                    }
                    if (smoothSync.isSyncingZRotation)
                    {
                        writer.Write(HalfHelper.Compress(rot.z * Mathf.Deg2Rad));
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXRotation)
                    {
                        writer.Write(rot.x);
                    }
                    if (smoothSync.isSyncingYRotation)
                    {
                        writer.Write(rot.y);
                    }
                    if (smoothSync.isSyncingZRotation)
                    {
                        writer.Write(rot.z);
                    }
                }
            }
            // Write scale.
            if (sendScale)
            {
                if (smoothSync.isScaleCompressed)
                {
                    if (smoothSync.isSyncingXScale)
                    {
                        writer.Write(HalfHelper.Compress(state.scale.x));
                    }
                    if (smoothSync.isSyncingYScale)
                    {
                        writer.Write(HalfHelper.Compress(state.scale.y));
                    }
                    if (smoothSync.isSyncingZScale)
                    {
                        writer.Write(HalfHelper.Compress(state.scale.z));
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXScale)
                    {
                        writer.Write(state.scale.x);
                    }
                    if (smoothSync.isSyncingYScale)
                    {
                        writer.Write(state.scale.y);
                    }
                    if (smoothSync.isSyncingZScale)
                    {
                        writer.Write(state.scale.z);
                    }
                }
            }
            // Write velocity.
            if (sendVelocity)
            {
                if (smoothSync.isVelocityCompressed)
                {
                    if (smoothSync.isSyncingXVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.velocity.x));
                    }
                    if (smoothSync.isSyncingYVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.velocity.y));
                    }
                    if (smoothSync.isSyncingZVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.velocity.z));
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXVelocity)
                    {
                        writer.Write(state.velocity.x);
                    }
                    if (smoothSync.isSyncingYVelocity)
                    {
                        writer.Write(state.velocity.y);
                    }
                    if (smoothSync.isSyncingZVelocity)
                    {
                        writer.Write(state.velocity.z);
                    }
                }
            }
            // Write angular velocity.
            if (sendAngularVelocity)
            {
                if (smoothSync.isAngularVelocityCompressed)
                {
                    // Convert to radians for more accurate Half numbers
                    if (smoothSync.isSyncingXAngularVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.angularVelocity.x * Mathf.Deg2Rad));
                    }
                    if (smoothSync.isSyncingYAngularVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.angularVelocity.y * Mathf.Deg2Rad));
                    }
                    if (smoothSync.isSyncingZAngularVelocity)
                    {
                        writer.Write(HalfHelper.Compress(state.angularVelocity.z * Mathf.Deg2Rad));
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXAngularVelocity)
                    {
                        writer.Write(state.angularVelocity.x);
                    }
                    if (smoothSync.isSyncingYAngularVelocity)
                    {
                        writer.Write(state.angularVelocity.y);
                    }
                    if (smoothSync.isSyncingZAngularVelocity)
                    {
                        writer.Write(state.angularVelocity.z);
                    }
                }
            }
            //// Only the server sends out owner information.
            //if (smoothSync.isSmoothingAuthorityChanges && NetworkServer.active)
            //{
            //    writer.Write((byte)smoothSync.ownerChangeIndicator);
            //}

            if (smoothSync.automaticallyResetTime)
            {
                writer.Write((byte)state.localTimeResetIndicator);
            }
        }

        /// <summary>
        /// Deserialize a message from the network.
        /// </summary>
        /// <remarks>
        /// Only receives what it needs and decompresses floats if you chose to.
        /// </remarks>
        /// <param name="writer">The Networkreader to read from.</param>
        public void Deserialize(BinaryReader reader, SmoothSyncPUN smoothSync)
        {
            // The first received byte tells us what we need to be syncing.
            byte syncInfoByte = reader.ReadByte();
            bool syncPosition = shouldSyncPosition(syncInfoByte);
            bool syncRotation = shouldSyncRotation(syncInfoByte);
            bool syncScale = shouldSyncScale(syncInfoByte);
            bool syncVelocity = shouldSyncVelocity(syncInfoByte);
            bool syncAngularVelocity = shouldSyncAngularVelocity(syncInfoByte);
            state.atPositionalRest = shouldBeAtPositionalRest(syncInfoByte);
            state.atRotationalRest = shouldBeAtRotationalRest(syncInfoByte);

            int syncIndex = (int)reader.ReadUInt32();
            state.ownerTimestamp = reader.ReadSingle();

            // If we want the server to relay non-owned object information out to other clients, set these variables so we know what we need to send.
            if (PhotonNetwork.isMasterClient && !smoothSync.photonView.isMine)
            {
                state.serverShouldRelayPosition = syncPosition;
                state.serverShouldRelayRotation = syncRotation;
                state.serverShouldRelayScale = syncScale;
                state.serverShouldRelayVelocity = syncVelocity;
                state.serverShouldRelayAngularVelocity = syncAngularVelocity;
            }

            // Find the correct object to sync according to the syncIndex.
            for (int i = 0; i < smoothSync.childObjectSmoothSyncs.Length; i++)
            {
                if (smoothSync.childObjectSmoothSyncs[i].syncIndex == syncIndex)
                {
                    smoothSync = smoothSync.childObjectSmoothSyncs[i];
                }
            }

            if (!smoothSync)
            {
                Debug.LogWarning("Could not find target for network state message.");
                return;
            }

            state.receivedTimestamp = smoothSync.localTime;

            if (smoothSync.receivedStatesCounter < PhotonNetwork.sendRateOnSerialize) smoothSync.receivedStatesCounter++;

            // Read position.
            if (syncPosition)
            {
                if (smoothSync.isPositionCompressed)
                {
                    if (smoothSync.isSyncingXPosition)
                    {
                        state.position.x = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingYPosition)
                    {
                        state.position.y = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingZPosition)
                    {
                        state.position.z = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXPosition)
                    {
                        state.position.x = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingYPosition)
                    {
                        state.position.y = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingZPosition)
                    {
                        state.position.z = reader.ReadSingle();
                    }
                }
            }
            else
            {
                if (smoothSync.stateCount > 0)
                {
                    state.position = smoothSync.stateBuffer[0].position;
                }
                else
                {
                    state.position = smoothSync.getPosition();
                }
            }

            // Read rotation.
            if (syncRotation)
            {
                state.reusableRotationVector = Vector3.zero;
                if (smoothSync.isRotationCompressed)
                {
                    if (smoothSync.isSyncingXRotation)
                    {
                        state.reusableRotationVector.x = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.x *= Mathf.Rad2Deg;
                    }
                    if (smoothSync.isSyncingYRotation)
                    {
                        state.reusableRotationVector.y = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.y *= Mathf.Rad2Deg;
                    }
                    if (smoothSync.isSyncingZRotation)
                    {
                        state.reusableRotationVector.z = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.z *= Mathf.Rad2Deg;
                    }
                    state.rotation = Quaternion.Euler(state.reusableRotationVector);
                }
                else
                {
                    if (smoothSync.isSyncingXRotation)
                    {
                        state.reusableRotationVector.x = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingYRotation)
                    {
                        state.reusableRotationVector.y = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingZRotation)
                    {
                        state.reusableRotationVector.z = reader.ReadSingle();
                    }
                    state.rotation = Quaternion.Euler(state.reusableRotationVector);
                }
            }
            else
            {
                if (smoothSync.stateCount > 0)
                {
                    state.rotation = smoothSync.stateBuffer[0].rotation;
                }
                else
                {
                    state.rotation = smoothSync.getRotation();
                }
            }
            // Read scale.
            if (syncScale)
            {
                if (smoothSync.isScaleCompressed)
                {
                    if (smoothSync.isSyncingXScale)
                    {
                        state.scale.x = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingYScale)
                    {
                        state.scale.y = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingZScale)
                    {
                        state.scale.z = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXScale)
                    {
                        state.scale.x = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingYScale)
                    {
                        state.scale.y = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingZScale)
                    {
                        state.scale.z = reader.ReadSingle();
                    }
                }
            }
            else
            {
                if (smoothSync.stateCount > 0)
                {
                    state.scale = smoothSync.stateBuffer[0].scale;
                }
                else
                {
                    state.scale = smoothSync.getScale();
                }
            }
            // Read velocity.
            if (syncVelocity)
            {
                if (smoothSync.isVelocityCompressed)
                {
                    if (smoothSync.isSyncingXVelocity)
                    {
                        state.velocity.x = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingYVelocity)
                    {
                        state.velocity.y = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                    if (smoothSync.isSyncingZVelocity)
                    {
                        state.velocity.z = HalfHelper.Decompress(reader.ReadUInt16());
                    }
                }
                else
                {
                    if (smoothSync.isSyncingXVelocity)
                    {
                        state.velocity.x = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingYVelocity)
                    {
                        state.velocity.y = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingZVelocity)
                    {
                        state.velocity.z = reader.ReadSingle();
                    }
                }
                smoothSync.latestReceivedVelocity = state.velocity;
            }
            else
            {
                // If we didn't receive an updated velocity, use the latest received velocity.
                state.velocity = smoothSync.latestReceivedVelocity;
            }
            // Read anguluar velocity.
            if (syncAngularVelocity)
            {
                if (smoothSync.isAngularVelocityCompressed)
                {
                    state.reusableRotationVector = Vector3.zero;
                    if (smoothSync.isSyncingXAngularVelocity)
                    {
                        state.reusableRotationVector.x = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.x *= Mathf.Rad2Deg;
                    }
                    if (smoothSync.isSyncingYAngularVelocity)
                    {
                        state.reusableRotationVector.y = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.y *= Mathf.Rad2Deg;
                    }
                    if (smoothSync.isSyncingZAngularVelocity)
                    {
                        state.reusableRotationVector.z = HalfHelper.Decompress(reader.ReadUInt16());
                        state.reusableRotationVector.z *= Mathf.Rad2Deg;
                    }
                    state.angularVelocity = state.reusableRotationVector;
                }
                else
                {
                    if (smoothSync.isSyncingXAngularVelocity)
                    {
                        state.angularVelocity.x = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingYAngularVelocity)
                    {
                        state.angularVelocity.y = reader.ReadSingle();
                    }
                    if (smoothSync.isSyncingZAngularVelocity)
                    {
                        state.angularVelocity.z = reader.ReadSingle();
                    }
                }
                smoothSync.latestReceivedAngularVelocity = state.angularVelocity;
            }
            else
            {
                // If we didn't receive an updated angular velocity, use the latest received angular velocity.
                state.angularVelocity = smoothSync.latestReceivedAngularVelocity;
            }

            //// Update new owner information sent from the Server.
            //if (smoothSync.isSmoothingAuthorityChanges && !NetworkServer.active)
            //{
            //    smoothSync.ownerChangeIndicator = (int)reader.ReadByte();
            //}

            if (smoothSync.automaticallyResetTime)
            {
                state.localTimeResetIndicator = (int)reader.ReadByte();
            }
        }
        /// <summary>
        /// Hardcoded information to determine position syncing.
        /// </summary>
        byte positionMask = 1;        // 0000_0001
        /// <summary>
        /// Hardcoded information to determine rotation syncing.
        /// </summary>
        byte rotationMask = 2;        // 0000_0010
        /// <summary>
        /// Hardcoded information to determine scale syncing.
        /// </summary>
        byte scaleMask = 4;        // 0000_0100
        /// <summary>
        /// Hardcoded information to determine velocity syncing.
        /// </summary>
        byte velocityMask = 8;        // 0000_1000
        /// <summary>
        /// Hardcoded information to determine angular velocity syncing.
        /// </summary>
        byte angularVelocityMask = 16; // 0001_0000
        /// <summary>
        /// Hardcoded information to determine whether the object is at rest and should stop extrapolating.
        /// </summary>
        byte atPositionalRestMask = 64; // 0100_0000
        /// <summary>
        /// Hardcoded information to determine whether the object is at rest and should stop extrapolating.
        /// </summary>
        byte atRotationalRestMask = 128; // 1000_0000
        /// <summary>
        /// Encode sync info based on what we want to send.
        /// </summary>
        byte encodeSyncInformation(bool sendPosition, bool sendRotation, bool sendScale, bool sendVelocity, bool sendAngularVelocity, bool atPositionalRest, bool atRotationalRest)
        {
            byte encoded = 0;

            if (sendPosition)
            {
                encoded = (byte)(encoded | positionMask);
            }
            if (sendRotation)
            {
                encoded = (byte)(encoded | rotationMask);
            }
            if (sendScale)
            {
                encoded = (byte)(encoded | scaleMask);
            }
            if (sendVelocity)
            {
                encoded = (byte)(encoded | velocityMask);
            }
            if (sendAngularVelocity)
            {
                encoded = (byte)(encoded | angularVelocityMask);
            }
            if (atPositionalRest)
            {
                encoded = (byte)(encoded | atPositionalRestMask);
            }
            if (atRotationalRest)
            {
                encoded = (byte)(encoded | atRotationalRestMask);
            }
            return encoded;
        }
        /// <summary>
        /// Decode sync info to see if we want to sync position.
        /// </summary>
        bool shouldSyncPosition(byte syncInformation)
        {
            if ((syncInformation & positionMask) == positionMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we want to sync rotation.
        /// </summary>
        bool shouldSyncRotation(byte syncInformation)
        {
            if ((syncInformation & rotationMask) == rotationMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we want to sync scale.
        /// </summary>
        bool shouldSyncScale(byte syncInformation)
        {
            if ((syncInformation & scaleMask) == scaleMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we want to sync velocity.
        /// </summary>
        bool shouldSyncVelocity(byte syncInformation)
        {
            if ((syncInformation & velocityMask) == velocityMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we want to sync angular velocity.
        /// </summary>
        bool shouldSyncAngularVelocity(byte syncInformation)
        {
            if ((syncInformation & angularVelocityMask) == angularVelocityMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we should be at positional rest. (Stop extrapolating)
        /// </summary>
        bool shouldBeAtPositionalRest(byte syncInformation)
        {
            if ((syncInformation & atPositionalRestMask) == atPositionalRestMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Decode sync info to see if we should be at rotational rest. (Stop extrapolating)
        /// </summary>
        bool shouldBeAtRotationalRest(byte syncInformation)
        {
            if ((syncInformation & atRotationalRestMask) == atRotationalRestMask)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}