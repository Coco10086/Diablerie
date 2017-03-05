﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
	public int directionCount = 8;
	public float speed = 3.5f;
	public float attackSpeed = 1.0f;
    public float useRange = 1f;
    public float attackRange = 2.5f;
    public float diameter = 1f;
    public bool run = false;

    public string basePath;
    public string token;
    public string weaponClass;
    public string[] gear;

    static float turnSpeed = 4f; // full rotations per second

    public delegate void TakeDamageHandler(Character originator, int damage);
    public event TakeDamageHandler OnTakeDamage;

    [HideInInspector]
    public GameObject target
    {
        get
        {
            return m_Target;
        }

        set
        {
            var usable = value.GetComponent<Usable>();
            if (usable != null)
                Use(usable);
            else
            {
                var targetCharacter = value.GetComponent<Character>();
                if (targetCharacter) {
                    Attack(targetCharacter);
                }
                else
                {
                    return;
                }
            }

            m_Target = value;
        }
    }

    [HideInInspector]
	public int directionIndex = 0;
    float direction = 0;

    Iso iso;
	COFAnimator animator;
    List<Pathing.Step> path = new List<Pathing.Step>();
	float traveled = 0;
	int desiredDirection = 0;
    bool moving = false;
	bool attack = false;
    bool takingDamage = false;
    bool dying = false;
    bool dead = false;
    GameObject m_Target;
    Usable usable;
    Character targetCharacter;
    public int attackDamage = 30;
    public int health = 100;
    public int maxHealth = 100;
    Vector2 targetPoint;

    void Awake()
    {
		iso = GetComponent<Iso>();
		animator = GetComponent<COFAnimator>();
    }

	public void Use(Usable usable) {
        if (attack || takingDamage || dying || dead)
            return;
        targetPoint = usable.GetComponent<Iso>().pos;
		this.usable = usable;
        targetCharacter = null;
        moving = true;
    }

    public void GoTo(Vector2 target)
    {
        if (attack || takingDamage || dying || dead)
            return;

        moving = true;
        targetPoint = target;
        usable = null;
        targetCharacter = null;
    }

    public void Teleport(Vector2 target) {
		if (attack || takingDamage)
			return;

		if (Tilemap.Passable(target)) {
			iso.pos = target;
		} else {
			var pathToTarget = Pathing.BuildPath(iso.pos, target, directionCount);
			if (pathToTarget.Count == 0)
				return;
			iso.pos = pathToTarget[pathToTarget.Count - 1].pos;
		}
		path.Clear();
		traveled = 0;
        moving = false;
	}

    public void Attack(Vector3 target)
    {
        if (!dead && !dying && !attack && !takingDamage && directionIndex == desiredDirection && !moving)
        {
            attack = true;
            targetPoint = target;
        }
    }

    public void Attack(Character targetCharacter)
    {
        if (attack || takingDamage || dead || dying)
            return;

        Iso targetIso = targetCharacter.GetComponent<Iso>();
        targetPoint = targetIso.pos;
        this.targetCharacter = targetCharacter;
        usable = null;
        moving = true;
    }

    void AbortPath()
    {
        m_Target = null;
        path.Clear();
        traveled = 0;
    }

	void Update() {
        if (!takingDamage && !dead && !dying) {
            if (usable)
            {
                var hit = Tilemap.Raycast(iso.pos, usable.GetComponent<Iso>().pos, maxRayLength: useRange + diameter / 2, ignore: gameObject);
                if (hit.gameObject == usable.gameObject)
                {
                    usable.Use();
                    moving = false;
                    usable = null;
                    m_Target = null;
                }
            }
            if (targetCharacter && !attack)
            {
                Vector2 target = targetCharacter.GetComponent<Iso>().pos;
                if (Vector2.Distance(target, iso.pos) <= attackRange + diameter / 2 + targetCharacter.diameter / 2)
                {
                    moving = false;
                    attack = true;
                    LookAtImmidietly(target);
                }
            }
        }

        MoveToTargetPoint();
        Turn();
	}

    void LateUpdate()
    {
        UpdateAnimation();
    }

    void Turn()
    {
        if (!dead && !dying && !attack && !takingDamage && directionIndex != desiredDirection)
        {
            float diff = Tools.ShortestDelta(directionIndex, desiredDirection, directionCount);
            float delta = Mathf.Abs(diff);
            direction += Mathf.Clamp(Mathf.Sign(diff) * turnSpeed * Time.deltaTime * directionCount, -delta, delta);
            direction = Tools.Mod(direction + directionCount, directionCount);
            directionIndex = Mathf.RoundToInt(direction) % directionCount;
        }
    }

	void MoveAlongPath() {
		if (path.Count == 0 || !moving || attack || takingDamage || dead || dying)
			return;

		Vector2 step = path[0].direction;
		float stepLen = step.magnitude;

        float distance = speed * Time.deltaTime;
		while (traveled + distance >= stepLen) {
			float firstPart = stepLen - traveled;
            Vector2 newPos = iso.pos + step.normalized * firstPart;
            iso.pos = newPos;
			distance -= firstPart;
			traveled += firstPart - stepLen;
			path.RemoveAt(0);
            if (path.Count > 0)
            {
                step = path[0].direction;
            }
		}
		if (path.Count > 0) {
			traveled += distance;
			iso.pos += step.normalized * distance;
		}

        if (path.Count == 0) {
			traveled = 0;
		}
        else
        {
            desiredDirection = path[0].directionIndex;
        }
    }

    void MoveToTargetPoint()
    {
        if (!moving)
            return;

        var newPath = Pathing.BuildPath(iso.pos, targetPoint, directionCount);
        if (newPath.Count == 0)
        {
            moving = false;
            return;
        }
        if (path.Count == 0 || newPath[newPath.Count - 1].pos != path[path.Count - 1].pos)
        {
            AbortPath();
            path.AddRange(newPath);
        }
        Pathing.DebugDrawPath(iso.pos, path);
        if (path.Count == 1 && Vector2.Distance(path[0].pos, targetPoint) < 1.0f)
        {
            var dir = (targetPoint - iso.pos).normalized;
            iso.pos += dir * Time.deltaTime * speed;
            desiredDirection = Iso.Direction(iso.pos, targetPoint, directionCount);
        }
        else
        {
            MoveAlongPath();
        }
    }

    void UpdateAnimation() {
        string mode;
        string weaponClass = this.weaponClass;
        animator.speed = 1.0f;
        animator.loop = true;
        if (dying || dead)
        {
            mode = "DT";
            weaponClass = "HTH";
            animator.loop = false;
        }
        else if (attack)
        {
            mode = "A1";
            animator.speed = attackSpeed;
        }
        else if (takingDamage)
        {
            mode = "GH";
            animator.loop = false;
        }
        else if (moving)
        {
            mode = run ? "RN" : "WL";
        }
        else
        {
            mode = "NU";
        }

        animator.cof = COF.Load(basePath, token, weaponClass, gear, mode);
        animator.direction = directionIndex;
    }

	public void LookAt(Vector3 target)
	{
        if (!moving)
            desiredDirection = Iso.Direction(iso.pos, target, directionCount);
    }

    public void LookAtImmidietly(Vector3 target)
    {
        directionIndex = desiredDirection = Iso.Direction(iso.pos, target, directionCount);
    }

    public void TakeDamage(Character originator, int damage)
    {
        health -= damage;
        if (health > 0)
        {
            if (OnTakeDamage != null)
                OnTakeDamage(originator, damage);
            takingDamage = true;
            attack = false;
        }
        else
        {
            LookAtImmidietly(originator.iso.pos);
            dying = true;
            attack = false;
        }
        moving = false;
        targetCharacter = null;
    }

    void OnAnimationMiddle()
    {
        if (attack)
        {
            if (targetCharacter == null)
            {
                var hit = Tilemap.Raycast(iso.pos, targetPoint, rayLength: diameter / 2 + attackRange, ignore: gameObject, debug: true);
                if (hit.gameObject != null)
                {
                    targetCharacter = hit.gameObject.GetComponent<Character>();
                }
            }

            if (targetCharacter)
            {
                Vector2 target = targetCharacter.GetComponent<Iso>().pos;
                if (Vector2.Distance(target, iso.pos) <= attackRange + diameter / 2 + targetCharacter.diameter / 2)
                {
                    targetCharacter.TakeDamage(this, attackDamage);
                }
                targetCharacter = null;
                m_Target = null;
            }
        }
    }

    void OnAnimationFinish()
    {
        attack = false;
        takingDamage = false;
        if (dying)
        {
            dying = false;
            dead = true;
        }
        UpdateAnimation();
    }
}
